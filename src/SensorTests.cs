using System;
using System.IO;

namespace CoolerPanel {
    internal static class SensorTests {
        static void Assert(bool condition,string name) { if(!condition) throw new Exception("Teste falhou: " + name); }
        internal static void Run() {
            const string fixture="[Source0]\nName=CPU temperature\nUnits=°C\nFormula=\n[Layer17]\nText=\"<S=100>%CPU temperature% \"\n[Source1]\nName=GPU1 temperature\nUnits=°C\nFormula=\n[Layer19]\nText=%GPU1 temperature%";
            var layout=Sensors.ParseIni(fixture);
            Assert(Sensors.FindLayer(layout,"CPU temperature",true)==17,"mapear CPU pelo arquivo, sem posição fixa");
            Assert(Sensors.FindLayer(layout,"GPU1 temperature",true)==19,"mapear GPU");
            Assert(Sensors.FindLayer(Sensors.ParseIni(fixture.Replace("Units=°C","Units=°F")),"CPU temperature",true)==-1,"rejeitar Fahrenheit");
            Assert(Sensors.FindLayer(Sensors.ParseIni(fixture.Replace("Formula=","Formula=x*2")),"CPU temperature",true)==-1,"rejeitar conversões");
            const string overlay="<L17><E=-5,-1,5><C1><S=100>40 <C><P=-6,-2><L19><C0>39.5 <C>";
            Assert(Sensors.Extract(overlay,17)==40 && Sensors.Extract(overlay,19)==39.5f,"distinguir temperatura de parâmetros dos tags");
            Assert(Sensors.Extract("<L17><S=100>42,5<C>",17)==42.5f,"decimal com vírgula");
            Assert(!Sensors.Extract("<L17><S=100>N/A<C>",17).HasValue,"ausência de leitura");
            Assert(!Sensors.Extract("<L17>42 W 12",17).HasValue,"não misturar métricas");
            Assert(!Sensors.Extract(overlay,18).HasValue,"camada inexistente");
            int invalid=0;
            foreach(float value in new[] { float.NaN,float.PositiveInfinity,-1f,0f,126f }) { try { Sensors.TemperatureByte(value); } catch(IOException) { invalid++; } }
            Assert(invalid==5,"rejeitar leituras impossíveis");
            Assert(Sensors.TemperatureByte(42.5f)==43 && Sensors.TemperatureByte(125)==125,"arredondamento e limite");
            Assert(Sensors.LoadByte(null)==0 && Sensors.LoadByte(101)==100 && Sensors.LoadByte(-1)==0,"limites da utilização");
        }
    }
}
