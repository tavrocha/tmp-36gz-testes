namespace IndustrialMonitor.Models
{
    public class PortaInfo
    {
        public string NomePorta { get; set; } = string.Empty;
        public string NomeExibicao { get; set; } = string.Empty;
        public bool IsEsp32 { get; set; }

        public override string ToString() => NomeExibicao;
    }
}