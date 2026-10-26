using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace CoolerPanel {
    internal sealed class Device {
        public string Path;
        public ushort OutputLength, InputLength, UsagePage, Usage;
        public override string ToString() { return string.Format("Painel AA88:8666 — saída {0} bytes, entrada {1} bytes", OutputLength, InputLength); }
    }

    internal static class Hid {
        static Device cachedDevice;
        const uint Present = 2, Interface = 16, Share = 3, OpenExisting = 3, OverlappedFlag = 0x40000000;
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public uint Size; public Guid Class; public uint Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct Attributes { public uint Size; public ushort Vendor, Product, Version; }
        [StructLayout(LayoutKind.Sequential)] struct Caps {
            public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
            public ushort Links, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
        }
        [StructLayout(LayoutKind.Sequential)] struct AsyncIO { public IntPtr Internal, InternalHigh; public uint Offset, OffsetHigh; public IntPtr Event; }
        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
        [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, out Caps caps);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr info, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(SafeFileHandle handle, IntPtr buffer, uint count, IntPtr written, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr overlapped, out uint count, bool wait);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateEvent(IntPtr security, bool manual, bool initial, string name);
        [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        internal static List<Device> Detect() {
            Guid guid; HidD_GetHidGuid(out guid);
            IntPtr info = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, Present | Interface);
            if (info == new IntPtr(-1)) throw new Win32Exception();
            var found = new List<Device>();
            try {
                for (uint i=0; ; i++) {
                    var entry = new InterfaceData { Size=(uint)Marshal.SizeOf(typeof(InterfaceData)) };
                    if (!SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref guid, i, ref entry)) {
                        int error=Marshal.GetLastWin32Error();
                        if (error==259) break;
                        throw new Win32Exception(error);
                    }
                    uint size;
                    SetupDiGetDeviceInterfaceDetail(info, ref entry, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    if (size < 8) continue;
                    IntPtr detail=Marshal.AllocHGlobal((int)size);
                    try {
                        Marshal.WriteInt32(detail, IntPtr.Size==8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(info, ref entry, detail, size, out size, IntPtr.Zero)) throw new Win32Exception();
                        string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                        if (path.IndexOf("vid_aa88&pid_8666", StringComparison.OrdinalIgnoreCase)<0) continue;
                        using (var handle=CreateFile(path,0,Share,IntPtr.Zero,OpenExisting,0,IntPtr.Zero)) {
                            if (handle.IsInvalid) {
                                int error=Marshal.GetLastWin32Error();
                                throw new Win32Exception(error, error==32
                                    ? "O programa original está mantendo o painel ocupado. Saia do CPU COOLER V3.0 pelo ícone ao lado do relógio e tente detectar novamente."
                                    : "Não foi possível consultar o painel: " + new Win32Exception(error).Message);
                            }
                            var attrs=new Attributes { Size=(uint)Marshal.SizeOf(typeof(Attributes)) };
                            if (!HidD_GetAttributes(handle,ref attrs)) throw new Win32Exception();
                            if (attrs.Vendor!=0xAA88 || attrs.Product!=0x8666) continue;
                            IntPtr data;
                            if (!HidD_GetPreparsedData(handle,out data)) throw new Win32Exception();
                            try {
                                Caps caps;
                                if (HidP_GetCaps(data,out caps)!=0x00110000) throw new IOException("Não foi possível ler o descritor HID.");
                                found.Add(new Device { Path=path, OutputLength=caps.OutputLength, InputLength=caps.InputLength, Usage=caps.Usage, UsagePage=caps.UsagePage });
                            } finally { HidD_FreePreparsedData(data); }
                        }
                    } finally { Marshal.FreeHGlobal(detail); }
                }
            } finally { SetupDiDestroyDeviceInfoList(info); }
            return found;
        }

        internal static byte[] Packet(int cpu, int gpu, int length) {
            if (cpu<0 || cpu>125 || gpu<0 || gpu>125) throw new ArgumentOutOfRangeException("Temperaturas suportadas: 0 a 125 °C.");
            // The live HID descriptor and the original producer both specify 11 bytes,
            // including report ID 0. The helper's 24-byte read buffer is not the report size.
            if (length!=11) throw new IOException("O painel declara " + length + " bytes. O protocolo confirmado usa 11; envio bloqueado até verificar a diferença.");
            byte[] packet=new byte[length]; packet[1]=(byte)cpu; packet[2]=(byte)gpu;
            return packet;
        }

        internal static string SendTest(int cpu, int gpu) {
            return Send(cpu,gpu,null,null,true);
        }
        internal static string SendLive(Reading reading) {
            return Send(Sensors.TemperatureByte(reading.Cpu),Sensors.TemperatureByte(reading.Gpu),reading.CpuLoad,reading.GpuLoad,false);
        }
        static string Send(int cpu,int gpu,float? cpuLoad,float? gpuLoad,bool simulated) {
            if(cachedDevice==null) {
                var devices=Detect();
                if (devices.Count!=1) throw new IOException(devices.Count==0 ? "Painel não encontrado. Confira a conexão USB interna." : "Há mais de uma interface compatível; é necessário identificar a correta antes de enviar.");
                cachedDevice=devices[0];
            }
            Device device=cachedDevice;
            byte[] packet=Packet(cpu,gpu,device.OutputLength);
            packet[3]=Sensors.LoadByte(cpuLoad); packet[6]=Sensors.LoadByte(gpuLoad);
            try {
            using (var handle=CreateFile(device.Path,0x40000000,Share,IntPtr.Zero,OpenExisting,OverlappedFlag,IntPtr.Zero)) {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"Não foi possível abrir o painel para envio. Feche o programa original se estiver em uso.");
                IntPtr buffer=Marshal.AllocHGlobal(packet.Length), operation=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AsyncIO)));
                IntPtr signal=CreateEvent(IntPtr.Zero,true,false,null);
                try {
                    if (signal==IntPtr.Zero) throw new Win32Exception();
                    Marshal.Copy(packet,0,buffer,packet.Length);
                    Marshal.StructureToPtr(new AsyncIO { Event=signal },operation,false);
                    if (!WriteFile(handle,buffer,(uint)packet.Length,IntPtr.Zero,operation)) {
                        int error=Marshal.GetLastWin32Error();
                        if (error!=997) throw new Win32Exception(error);
                        if (WaitForSingleObject(signal,2000)!=0) {
                            CancelIoEx(handle,operation);
                            uint discarded; GetOverlappedResult(handle,operation,out discarded,true);
                            throw new IOException("O painel não concluiu o envio em 2 segundos.");
                        }
                    }
                    uint written;
                    if (!GetOverlappedResult(handle,operation,out written,true)) throw new Win32Exception();
                    if (written!=packet.Length) throw new IOException("O envio foi incompleto.");
                } finally {
                    if (signal!=IntPtr.Zero) CloseHandle(signal);
                    Marshal.FreeHGlobal(operation); Marshal.FreeHGlobal(buffer);
                }
            }
            } catch { cachedDevice=null; throw; }
            return "Envio USB concluído: " + packet.Length + " bytes.\r\nCPU: " + cpu + " °C | GPU: " + gpu + " °C" + (simulated ? " (valores simulados)." : " (temperaturas reais via RivaTuner).") + "\r\nPacote: " + BitConverter.ToString(packet);
        }
    }

    internal static class Program {
        internal static string Probe() {
            var devices=Hid.Detect();
            if (devices.Count==0) return "Painel AA88:8666 não encontrado. Confira a conexão USB interna.";
            return string.Join("\r\n\r\n",devices.Select(d => d + "\r\nUso HID: " + d.UsagePage.ToString("X4") + ":" + d.Usage.ToString("X4") + "\r\n" + d.Path));
        }
        static string SelfTest() {
            byte[] packet=Hid.Packet(42,37,11);
            if (packet.Length!=11 || packet[0]!=0 || packet[1]!=42 || packet[2]!=37 || packet.Skip(3).Any(b=>b!=0)) throw new Exception("Pacote incorreto.");
            int rejected=0;
            foreach (int size in new[] { 0,10,12,24,64,65 }) { try { Hid.Packet(42,37,size); } catch(IOException) { rejected++; } }
            if (rejected!=6) throw new Exception("Validação de tamanho falhou.");
            rejected=0;
            foreach (int value in new[] { -1,126,256 }) { try { Hid.Packet(value,37,11); } catch(ArgumentOutOfRangeException) { rejected++; } }
            try { Hid.Packet(42,126,11); } catch(ArgumentOutOfRangeException) { rejected++; }
            if (rejected!=4) throw new Exception("Validação de temperatura falhou.");
            Hid.Packet(0,125,11); Hid.Packet(125,0,11);
            SensorTests.Run();
            return "PASS: pacote HID, limites de temperatura, mapeamento do layout RTSS, extração de valores e rejeição de leituras inválidas.";
        }
        internal static void Monitor(IProgress<string> progress,CancellationToken cancellation,int count) {
            using(var sensors=new Sensors()) {
                for(int sent=0; count==0 || sent<count; sent++) {
                    cancellation.ThrowIfCancellationRequested();
                    Reading reading=sensors.Read(cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    string result=Hid.SendLive(reading);
                    if(progress!=null) progress.Report(DateTime.Now.ToString("HH:mm:ss") + " — atualização " + (sent+1) + "\r\n" + reading + "\r\n" + result);
                    if(count==0 || sent+1<count) { if(cancellation.WaitHandle.WaitOne(1000)) cancellation.ThrowIfCancellationRequested(); }
                }
            }
        }
        [STAThread] static int Main(string[] args) {
            if (args.Length==0 || args[0]=="--tray") { return RunApplication(args.Length>0); }
            string log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resultado-teste.txt");
            try {
                string result;
                switch(args[0]) {
                    case "--probe": result=Probe(); break;
                    case "--self-test": result=SelfTest(); break;
                    case "--startup-enable": Startup.SetEnabled(true); result="Inicialização automática ativada: " + Startup.Command; break;
                    case "--startup-status": result="Inicialização automática: " + Startup.IsEnabled; break;
                    case "--send-test": result=Hid.SendTest(42,37); break;
                    case "--sensors": using(var sensors=new Sensors()) result=sensors.Read(CancellationToken.None).ToString(); break;
                    case "--live-test":
                        var updates=new StringBuilder();
                        Monitor(new InlineProgress(message=>updates.AppendLine(message)),CancellationToken.None,15);
                        result=updates.ToString(); break;
                    default: throw new ArgumentException("Opções: --probe, --self-test, --send-test, --sensors, --live-test");
                }
                File.WriteAllText(log,result,Encoding.UTF8); return 0;
            } catch(Exception error) { File.WriteAllText(log,"FALHA: " + error.Message,Encoding.UTF8); return 1; }
        }
        static int RunApplication(bool hidden) {
            bool created;
            using(var instance=new Mutex(true,@"Local\CoolerPanel.App",out created)) {
                if(!created) {
                    if(hidden) return 0;
                    try { using(var signal=EventWaitHandle.OpenExisting(@"Local\CoolerPanel.Show")) signal.Set(); } catch(WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\CoolerPanel.Show")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using(var window=new MainWindow(hidden)) {
                        IntPtr handle=window.Handle;
                        window.EnableTray();
                        RegisteredWaitHandle registration=ThreadPool.RegisterWaitForSingleObject(signal,delegate {
                            try { if(!window.IsDisposed) window.BeginInvoke((Action)window.ShowPanel); } catch(InvalidOperationException) { }
                        },null,Timeout.Infinite,false);
                        try { window.BeginInvoke((Action)window.StartMonitoring); Application.Run(window); }
                        finally { registration.Unregister(null); }
                    }
                }
                instance.ReleaseMutex();
            }
            return 0;
        }
        sealed class InlineProgress : IProgress<string> {
            readonly Action<string> action;
            public InlineProgress(Action<string> action) { this.action=action; }
            public void Report(string value) { action(value); }
        }
    }
}
