using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoolerPanel {
    internal sealed class MainWindow : Form {
        static readonly Color Ink=Color.FromArgb(27,55,67), Muted=Color.FromArgb(85,108,119), Accent=Color.FromArgb(22,116,126);
        readonly Label cpuValue=new Label(),gpuValue=new Label(),state=new Label();
        readonly Button start=ButtonFor("Iniciar",true),stop=ButtonFor("Parar",false),read=ButtonFor("Ler temperaturas",false),detect=ButtonFor("Detectar painel",false),send=ButtonFor("Enviar teste",false);
        readonly NumericUpDown cpu=new NumericUpDown { Minimum=0,Maximum=100,Value=42,Width=56 },gpu=new NumericUpDown { Minimum=0,Maximum=100,Value=37,Width=56 };
        readonly CheckBox startup=new CheckBox { Text="Iniciar com o Windows",AutoSize=true };
        readonly NotifyIcon tray=new NotifyIcon();
        readonly ToolStripMenuItem trayStart=new ToolStripMenuItem("Iniciar monitoramento"),trayStop=new ToolStripMenuItem("Parar monitoramento");
        CancellationTokenSource cancellation;
        Task work;
        bool exiting,firstVisibility=true,startHidden,busy;
        public MainWindow(bool hidden) {
            startHidden=hidden; Text="Cooler Panel"; Icon=Icon.ExtractAssociatedIcon(typeof(MainWindow).Assembly.Location);
            ClientSize=new Size(480,470); MinimumSize=MaximumSize=Size; MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",9); ForeColor=Ink; BackColor=Color.FromArgb(245,248,250);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24,20,24,16),ColumnCount=1,RowCount=7 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new[] {38,74,24,38,52,38}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.Controls.Add(new Label { Text="Cooler Panel",AutoSize=true,Font=new Font("Segoe UI Semibold",19),Margin=Padding.Empty });
            var temperatures=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.White,Padding=new Padding(12,6,8,5),Margin=Padding.Empty };
            temperatures.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); temperatures.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            temperatures.RowStyles.Add(new RowStyle(SizeType.Absolute,20)); temperatures.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            temperatures.Controls.Add(new Label { Text="CPU",AutoSize=true,ForeColor=Muted },0,0); temperatures.Controls.Add(new Label { Text="GPU",AutoSize=true,ForeColor=Muted },1,0);
            foreach(var label in new[] {cpuValue,gpuValue}) { label.Text="— °C"; label.AutoSize=true; label.Font=new Font("Consolas",25); label.Margin=Padding.Empty; }
            temperatures.Controls.Add(cpuValue,0,1); temperatures.Controls.Add(gpuValue,1,1); root.Controls.Add(temperatures);
            root.Controls.Add(new Label { Text="Sensores do RivaTuner · atualização a cada segundo",AutoSize=true,ForeColor=Muted,Margin=new Padding(0,7,0,0) });
            var mainActions=Flow(); mainActions.Controls.Add(start); mainActions.Controls.Add(stop); root.Controls.Add(mainActions);
            state.Text="Pronto para monitorar."; state.Dock=DockStyle.Fill; state.AutoEllipsis=true; state.Padding=new Padding(0,7,0,0); state.Margin=Padding.Empty; root.Controls.Add(state);
            var tools=Flow(); tools.Controls.Add(read); tools.Controls.Add(detect); root.Controls.Add(tools);
            var bottom=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty };
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
            var testing=Flow(); testing.Controls.Add(new Label { Text="Teste (°C)",AutoSize=true,Padding=new Padding(0,5,0,0) }); testing.Controls.Add(cpu); testing.Controls.Add(gpu); testing.Controls.Add(send); bottom.Controls.Add(testing);
            startup.Checked=Startup.IsEnabled; startup.Margin=new Padding(0,10,0,0); bottom.Controls.Add(startup); root.Controls.Add(bottom); Controls.Add(root);
            stop.Enabled=false;
            start.Click+=delegate { StartMonitoring(); }; stop.Click+=delegate { StopMonitoring(); };
            read.Click+=delegate { BeginCommand(true,false); }; detect.Click+=delegate { BeginCommand(false,false); }; send.Click+=delegate { BeginCommand(false,true); };
            startup.CheckedChanged+=delegate { try { Startup.SetEnabled(startup.Checked); } catch(Exception error) { state.Text="Não foi possível salvar a inicialização: " + error.Message; } };
            FormClosing+=delegate(object sender,FormClosingEventArgs e) {
                if(e.CloseReason==CloseReason.UserClosing && !exiting) { e.Cancel=true; Hide(); ShowInTaskbar=false; }
                else { exiting=true; if(cancellation!=null) cancellation.Cancel(); tray.Visible=false; }
            };
            Resize+=delegate { if(WindowState==FormWindowState.Minimized) { Hide(); ShowInTaskbar=false; } };
        }
        static Button ButtonFor(string text,bool primary) {
            var button=new Button { Text=text,AutoSize=true,Height=32,MinimumSize=new Size(78,32),FlatStyle=FlatStyle.Flat,BackColor=primary ? Accent : Color.White,ForeColor=primary ? Color.White : Ink,Margin=new Padding(0,0,8,0),Padding=new Padding(8,0,8,0) };
            button.FlatAppearance.BorderSize=primary ? 0 : 1; button.FlatAppearance.BorderColor=Color.FromArgb(211,222,228); return button;
        }
        static FlowLayoutPanel Flow() { return new FlowLayoutPanel { Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty,WrapContents=false }; }
        protected override void SetVisibleCore(bool value) {
            if(firstVisibility) { firstVisibility=false; if(startHidden) value=false; }
            base.SetVisibleCore(value);
        }
        internal void EnableTray() {
            var menu=new ContextMenuStrip();
            menu.Items.Add("Abrir",null,delegate { ShowPanel(); }); menu.Items.Add(new ToolStripSeparator());
            trayStart.Click+=delegate { StartMonitoring(); }; trayStop.Click+=delegate { StopMonitoring(); };
            menu.Items.Add(trayStart); menu.Items.Add(trayStop); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Sair",null,async delegate { await ExitApplication(); });
            tray.ContextMenuStrip=menu; tray.Icon=Icon; tray.Text="Cooler Panel"; tray.Visible=true;
            tray.DoubleClick+=delegate { ShowPanel(); };
        }
        internal void ShowPanel() { ShowInTaskbar=true; Show(); WindowState=FormWindowState.Normal; Activate(); }
        void SetBusy(bool value) {
            busy=value; start.Enabled=read.Enabled=detect.Enabled=send.Enabled=cpu.Enabled=gpu.Enabled=!value;
            trayStart.Enabled=!value; stop.Enabled=trayStop.Enabled=value && cancellation!=null;
        }
        internal void StartMonitoring() {
            if(busy || exiting) return;
            cancellation=new CancellationTokenSource(); SetBusy(true); work=MonitorAsync();
        }
        void StopMonitoring() { if(cancellation!=null) cancellation.Cancel(); }
        async Task MonitorAsync() {
            var progress=new Progress<Reading>(reading=> {
                if(exiting) return;
                cpuValue.Text=Sensors.TemperatureByte(reading.Cpu)+" °C"; gpuValue.Text=Sensors.TemperatureByte(reading.Gpu)+" °C";
                state.Text="Monitorando · você pode fechar esta janela."; state.ForeColor=Muted;
                tray.Text="Cooler Panel · CPU " + Sensors.TemperatureByte(reading.Cpu) + " °C · GPU " + Sensors.TemperatureByte(reading.Gpu) + " °C";
            });
            var status=new Progress<string>(message=> { if(!exiting) { state.Text=message; state.ForeColor=Muted; tray.Text="Cooler Panel · aguardando conexão"; cpuValue.Text=gpuValue.Text="— °C"; } });
            try { await Task.Run(()=> {
                while(true) {
                    cancellation.Token.ThrowIfCancellationRequested();
                    try {
                        using(var sensors=new Sensors()) {
                            while(true) {
                                Reading reading=sensors.Read(cancellation.Token);
                                cancellation.Token.ThrowIfCancellationRequested();
                                Hid.SendLive(reading); ((IProgress<Reading>)progress).Report(reading);
                                if(cancellation.Token.WaitHandle.WaitOne(1000)) cancellation.Token.ThrowIfCancellationRequested();
                            }
                        }
                    } catch(OperationCanceledException) { throw; }
                    catch(Exception error) {
                        ((IProgress<string>)status).Report("Aguardando RivaTuner ou painel. Nova tentativa em 5 s.\n" + error.Message);
                        if(cancellation.Token.WaitHandle.WaitOne(5000)) cancellation.Token.ThrowIfCancellationRequested();
                    }
                }
            }); }
            catch(OperationCanceledException) { state.Text="Monitoramento parado."; tray.Text="Cooler Panel · parado"; }
            finally { cancellation.Dispose(); cancellation=null; SetBusy(false); }
        }
        void BeginCommand(bool temperatures,bool test) {
            if(busy || exiting) return;
            int c=(int)cpu.Value,g=(int)gpu.Value; SetBusy(true); work=CommandAsync(temperatures,test,c,g);
        }
        async Task CommandAsync(bool temperatures,bool test,int c,int g) {
            try {
                state.Text="Consultando...";
                Reading reading=null;
                string result=await Task.Run(()=> {
                    if(temperatures) { using(var sensors=new Sensors()) reading=sensors.Read(CancellationToken.None); return "Temperaturas lidas do RivaTuner."; }
                    return test ? Hid.SendTest(c,g) : Program.Probe();
                });
                if(reading!=null) { cpuValue.Text=Sensors.TemperatureByte(reading.Cpu)+" °C"; gpuValue.Text=Sensors.TemperatureByte(reading.Gpu)+" °C"; }
                state.Text=test ? "Teste enviado: CPU " + c + " °C / GPU " + g + " °C (simulados)." : result.Split(new[] {"\r\n"},StringSplitOptions.None)[0];
            } catch(Exception error) { state.Text=error.Message; }
            finally { SetBusy(false); }
        }
        async Task ExitApplication() { if(exiting) return; exiting=true; StopMonitoring(); if(work!=null) await work; tray.Visible=false; Close(); }
        protected override void Dispose(bool disposing) { if(disposing) { tray.Visible=false; if(tray.ContextMenuStrip!=null) tray.ContextMenuStrip.Dispose(); tray.Dispose(); } base.Dispose(disposing); }
    }
}
