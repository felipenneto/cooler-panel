using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CoolerPanel {
    internal sealed class Reading {
        public float Cpu, Gpu;
        public float? CpuLoad, GpuLoad;
        public override string ToString() {
            return string.Format("CPU: {0:0} °C | GPU: {1:0} °C\r\nUso CPU: {2} | GPU: {3}\r\nFonte: RivaTuner / OverlayEditor (temperaturas reais).",Cpu,Gpu,
                CpuLoad.HasValue ? CpuLoad.Value.ToString("0") + "%" : "indisponível",GpuLoad.HasValue ? GpuLoad.Value.ToString("0") + "%" : "indisponível");
        }
    }

    // Reads the documented RTSS shared-memory OSD slots. Never writes to RTSS,
    // opens a hardware driver or changes the user's overlay configuration.
    internal sealed class Sensors : IDisposable {
        readonly string configPath, layoutPath;
        readonly DateTime configVersion, layoutVersion;
        readonly int cpuLayer, gpuLayer, cpuLoadLayer, gpuLoadLayer;
        uint lastFrame; bool seenFrame, fresh;
        readonly Stopwatch sinceUpdate=new Stopwatch();
        MemoryMappedFile sharedMemory;
        MemoryMappedViewAccessor sharedView;
        readonly byte[] textBuffer=new byte[32768];
        public Sensors() {
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"RivaTuner Statistics Server","Plugins","Client");
            configPath=Path.Combine(directory,"OverlayEditor.cfg");
            var config=ParseIni(File.ReadAllText(configPath,Encoding.GetEncoding(1252)));
            string layout=Get(config,"Settings","Layout");
            if (string.IsNullOrEmpty(layout) || Path.GetFileName(layout)!=layout) throw new IOException("Layout do RivaTuner não reconhecido.");
            layoutPath=Path.Combine(directory,"Overlays",layout);
            var definitions=ParseIni(File.ReadAllText(layoutPath,Encoding.GetEncoding(1252)));
            configVersion=File.GetLastWriteTimeUtc(configPath); layoutVersion=File.GetLastWriteTimeUtc(layoutPath);
            cpuLayer=FindLayer(definitions,"CPU temperature",true);
            gpuLayer=FindLayer(definitions,"GPU1 temperature",true);
            cpuLoadLayer=FindLayer(definitions,"CPU usage",false);
            gpuLoadLayer=FindLayer(definitions,"GPU1 usage",false);
            if (cpuLayer<0 || gpuLayer<0) throw new IOException("O layout ativo precisa conter CPU temperature e GPU1 temperature em °C, sem fórmulas de conversão.");
        }
        internal static Dictionary<string,Dictionary<string,string>> ParseIni(string text) {
            var result=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string,string> section=null;
            foreach (string raw in text.Split('\n')) {
                string line=raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { section=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); result[line.Substring(1,line.Length-2)]=section; }
                else if (section!=null && !line.StartsWith(";")) {
                    int split=line.IndexOf('='); if(split>0) section[line.Substring(0,split).Trim()]=line.Substring(split+1).Trim().Trim('"');
                }
            }
            return result;
        }
        static string Get(Dictionary<string,Dictionary<string,string>> data,string section,string key) {
            Dictionary<string,string> values; string value;
            return data.TryGetValue(section,out values) && values.TryGetValue(key,out value) ? value : "";
        }
        internal static int FindLayer(Dictionary<string,Dictionary<string,string>> data,string source,bool temperature) {
            string definition=data.Keys.FirstOrDefault(k=>k.StartsWith("Source",StringComparison.OrdinalIgnoreCase) && Get(data,k,"Name")==source);
            if(definition==null || Get(data,definition,"Formula")!="") return -1;
            string units=Get(data,definition,"Units");
            if(temperature && units!="°C") return -1;
            if(!temperature && units!="%") return -1;
            foreach(string key in data.Keys.Where(k=>k.StartsWith("Layer",StringComparison.OrdinalIgnoreCase))) {
                string text=Regex.Replace(Get(data,key,"Text"),"<[^>]*>","").Trim();
                int index;
                if(text=="%"+source+"%" && int.TryParse(key.Substring(5),out index)) return index;
            }
            return -1;
        }
        internal static float? Extract(string text,int layer) {
            if(layer<0) return null;
            Match match=Regex.Match(text,"<L"+layer+@">(.*?)(?=<L\d+>|$)",RegexOptions.Singleline);
            if(!match.Success) return null;
            string scalar=Regex.Replace(match.Groups[1].Value,"<[^>]*>","").Trim();
            if(!Regex.IsMatch(scalar,@"^\d+(?:[.,]\d+)?$")) return null;
            float value;
            return float.TryParse(scalar.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value) && !float.IsInfinity(value) ? (float?)value : null;
        }
        string ReadString(MemoryMappedViewAccessor view,long offset,int length) {
            view.ReadArray(offset,textBuffer,0,length);
            int end=Array.IndexOf(textBuffer,(byte)0,0,length);
            return Encoding.GetEncoding(1252).GetString(textBuffer,0,end<0 ? length : end);
        }
        string Snapshot(out uint frame) {
            if(sharedMemory==null) {
                sharedMemory=MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2",MemoryMappedFileRights.Read);
                try { sharedView=sharedMemory.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read); }
                catch { sharedMemory.Dispose(); sharedMemory=null; throw; }
            }
            var view=sharedView;
                uint version=view.ReadUInt32(4),size=view.ReadUInt32(20),offset=view.ReadUInt32(24),count=view.ReadUInt32(28);
                if(view.ReadUInt32(0)!=0x52545353 || version<0x00020007 || size<4608 || size>1048576 || count>256 || (long)offset+(long)size*count>view.Capacity) throw new IOException("Formato da memória do RTSS incompatível.");
                bool busy=version>=0x0002000e && (view.ReadUInt32(36)&1)!=0;
                frame=view.ReadUInt32(32);
                if(busy) return null;
                string overlay=null;
                for(uint index=0; index<count; index++) {
                    long entry=(long)offset+(long)size*index;
                    if(ReadString(view,entry+256,256)!="RTSSOverlayEditor") continue;
                    if(overlay!=null) throw new IOException("Mais de um overlay de sensores foi encontrado.");
                    overlay=version>=0x00020014 && size>=299520 ? ReadString(view,entry+266752,32768) : "";
                    if(overlay.Length==0) overlay=ReadString(view,entry+512,4096);
                }
                if(view.ReadUInt32(32)!=frame || (version>=0x0002000e && (view.ReadUInt32(36)&1)!=0)) return null;
                if(string.IsNullOrEmpty(overlay)) throw new IOException("O RivaTuner não está publicando o overlay de sensores. Ative o OverlayEditor e seu layout.");
                return overlay;
        }
        internal Reading Read(CancellationToken cancellation) {
            if(File.GetLastWriteTimeUtc(configPath)!=configVersion || File.GetLastWriteTimeUtc(layoutPath)!=layoutVersion) throw new IOException("O layout do RivaTuner mudou. Pare e reinicie o monitoramento para reler o mapeamento.");
            var waiting=Stopwatch.StartNew();
            while(true) {
                cancellation.ThrowIfCancellationRequested();
                uint frame; string text=Snapshot(out frame);
                if(text!=null) {
                    if(!seenFrame) { seenFrame=true; lastFrame=frame; sinceUpdate.Restart(); }
                    else if(frame!=lastFrame) { fresh=true; lastFrame=frame; sinceUpdate.Restart(); }
                    if(sinceUpdate.Elapsed.TotalSeconds>10) throw new IOException("O RivaTuner parou de atualizar o overlay. O envio foi interrompido para não repetir temperaturas antigas.");
                    if(fresh) {
                        float? cpu=Extract(text,cpuLayer),gpu=Extract(text,gpuLayer);
                        if(!cpu.HasValue || !gpu.HasValue) throw new IOException("Temperatura ausente no overlay. Nenhum valor simulado será enviado.");
                        TemperatureByte(cpu.Value); TemperatureByte(gpu.Value);
                        return new Reading { Cpu=cpu.Value,Gpu=gpu.Value,CpuLoad=Extract(text,cpuLoadLayer),GpuLoad=Extract(text,gpuLoadLayer) };
                    }
                }
                if(waiting.Elapsed.TotalSeconds>4) throw new IOException("O overlay do RivaTuner não está atualizando. Deixe o monitoramento de sensores ativo e tente novamente.");
                cancellation.WaitHandle.WaitOne(100);
            }
        }
        internal static int TemperatureByte(float value) {
            if(float.IsNaN(value) || float.IsInfinity(value) || value<=0 || value>125) throw new IOException("Leitura de temperatura inválida; envio interrompido.");
            return (int)Math.Round(value,MidpointRounding.AwayFromZero);
        }
        internal static byte LoadByte(float? value) { return value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) ? (byte)Math.Max(0,Math.Min(100,Math.Round(value.Value))) : (byte)0; }
        public void Dispose() { if(sharedView!=null) sharedView.Dispose(); if(sharedMemory!=null) sharedMemory.Dispose(); }
    }
}
