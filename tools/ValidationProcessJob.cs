using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OracleValidation
{
    // Windows applies the job at process creation, before the console wrapper
    // can launch Godot. Descendants inherit its aggregate limits and lifetime.
    // https://devblogs.microsoft.com/oldnewthing/20230209-00/?p=107812
    public sealed class ValidationProcessJob : IDisposable
    {
        private readonly SafeFileHandle job;

        public ValidationProcessJob(ulong memoryBytes, uint cpuPercent)
        {
            job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) throw new Win32Exception();
            try
            {
                var limits = new ExtendedLimits();
                limits.Basic.LimitFlags = 0x2000 | 0x200 | 0x20; // Kill on close, job memory, priority.
                limits.Basic.PriorityClass = 0x4000; // Below normal.
                limits.JobMemoryLimit = new UIntPtr(memoryBytes);
                SetInformation(9, limits);
                SetInformation(15, new CpuLimits { Flags = 5, Rate = cpuPercent * 100 });
            }
            catch { job.Dispose(); throw; }
        }

        public ulong PeakMemoryBytes
        {
            get
            {
                ExtendedLimits limits;
                if (!QueryInformationJobObject(job, 9, out limits, Marshal.SizeOf(typeof(ExtendedLimits)), IntPtr.Zero))
                    throw new Win32Exception();
                return limits.PeakJobMemoryUsed.ToUInt64();
            }
        }

        public WorkerProcess Start(string executable, string arguments, string directory, string stdout, string stderr)
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), InheritHandle = 1 };
            using (var output = OpenFile(stdout, 0x40000000, 2, ref security))
            using (var error = OpenFile(stderr, 0x40000000, 2, ref security))
            using (var input = OpenFile("NUL", 0x80000000, 3, ref security))
            {
                IntPtr size = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
                IntPtr attributes = Marshal.AllocHGlobal(size);
                IntPtr handles = Marshal.AllocHGlobal(3 * IntPtr.Size);
                IntPtr jobs = Marshal.AllocHGlobal(IntPtr.Size);
                bool initialized = false;
                try
                {
                    if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) throw new Win32Exception();
                    initialized = true;
                    Marshal.WriteIntPtr(jobs, job.DangerousGetHandle());
                    Marshal.WriteIntPtr(handles, 0, output.DangerousGetHandle());
                    Marshal.WriteIntPtr(handles, IntPtr.Size, error.DangerousGetHandle());
                    Marshal.WriteIntPtr(handles, 2 * IntPtr.Size, input.DangerousGetHandle());
                    if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x2000d), jobs, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero) ||
                        !UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), handles, new IntPtr(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                        throw new Win32Exception();
                    var startup = new StartupInfoEx();
                    startup.Info.Size = Marshal.SizeOf(typeof(StartupInfoEx));
                    startup.Info.Flags = 0x101; // STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW (hidden).
                    startup.Info.Input = input.DangerousGetHandle();
                    startup.Info.Output = output.DangerousGetHandle();
                    startup.Info.Error = error.DangerousGetHandle();
                    startup.Attributes = attributes;
                    ProcessInformation created;
                    if (!CreateProcess(executable, new StringBuilder("\"" + executable + "\" " + arguments),
                        IntPtr.Zero, IntPtr.Zero, true, 0x08080000, IntPtr.Zero, directory, ref startup, out created))
                        throw new Win32Exception();
                    // Retain the creation handle so status remains readable
                    // even when the worker exits immediately.
                    CloseHandle(created.Thread);
                    return new WorkerProcess(created.Process, created.Id);
                }
                finally
                {
                    if (initialized) DeleteProcThreadAttributeList(attributes);
                    Marshal.FreeHGlobal(attributes);
                    Marshal.FreeHGlobal(handles);
                    Marshal.FreeHGlobal(jobs);
                    GC.KeepAlive(job);
                }
            }
        }

        public sealed class WorkerProcess : IDisposable
        {
            private readonly SafeFileHandle creationHandle;
            public WorkerProcess(IntPtr handle, int id)
            {
                creationHandle = new SafeFileHandle(handle, true);
                Id = id;
            }
            public int Id { get; private set; }
            public bool HasExited
            {
                get
                {
                    uint result = WaitForSingleObject(creationHandle, 0);
                    if (result == 0xffffffff) throw new Win32Exception();
                    return result == 0;
                }
            }
            public int ExitCode
            {
                get { uint code; if (!GetExitCodeProcess(creationHandle, out code)) throw new Win32Exception(); return unchecked((int)code); }
            }
            public void WaitForExit()
            {
                if (WaitForSingleObject(creationHandle, 0xffffffff) == 0xffffffff) throw new Win32Exception();
            }
            public void Dispose() { creationHandle.Dispose(); }
        }

        public void Dispose() { job.Dispose(); }

        private void SetInformation<T>(int kind, T value) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, pointer, false);
                if (!SetInformationJobObject(job, kind, pointer, size)) throw new Win32Exception();
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        private static SafeFileHandle OpenFile(string path, uint access, uint disposition, ref SecurityAttributes security)
        {
            var handle = CreateFile(path, access, 3, ref security, disposition, 0x80, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(); }
            return handle;
        }

        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            public long ProcessTime, JobTime;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcesses;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        [StructLayout(LayoutKind.Sequential)] private struct CpuLimits { public uint Flags, Rate; }
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int InheritHandle; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
        {
            public int Size;
            public string Reserved, Desktop, Title;
            public int X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
            public short ShowWindow, ReservedSize;
            public IntPtr ReservedData, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public int Id, ThreadId; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, IntPtr info, int size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out ExtendedLimits info, int size, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(SafeFileHandle handle, out uint code);
    }
}
