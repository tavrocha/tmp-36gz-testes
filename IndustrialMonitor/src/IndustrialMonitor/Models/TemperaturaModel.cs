using System;

namespace IndustrialMonitor.Models
{
    public class TemperaturaModel
    {
        public double Valor { get; set; }
        public DateTime Timestamp { get; set; }

        public TemperaturaModel(double valor)
        {
            Valor = valor;
            Timestamp = DateTime.Now;
        }
    }
}