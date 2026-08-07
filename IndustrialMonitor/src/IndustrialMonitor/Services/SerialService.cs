using System;
using System.IO.Ports;
using System.Linq;

namespace IndustrialMonitor.Services
{
    public class SerialService
    {
        private SerialPort? _serialPort;

        public event Action<double>? TemperaturaRecebida;
        public event Action<bool>? StatusConexaoAlterado;

        public bool IsConectado => _serialPort?.IsOpen ?? false;

        public string[] ObterPortasDisponiveis()
        {
            return SerialPort.GetPortNames().Distinct().ToArray();
        }

        public bool Conectar(string porta, int baudRate = 115200)
        {
            if (IsConectado) Disconectar();

            try
            {
                _serialPort = new SerialPort(porta, baudRate)
                {
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                
                StatusConexaoAlterado?.Invoke(true);
                return true;
            }
            catch
            {
                StatusConexaoAlterado?.Invoke(false);
                return false;
            }
        }

        public void Disconectar()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
            StatusConexaoAlterado?.Invoke(false);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                string linha = _serialPort.ReadLine().Trim();

                if (linha.StartsWith("TEMP="))
                {
                    string valorTexto = linha.Replace("TEMP=", "").Trim();
                    if (double.TryParse(valorTexto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temperatura))
                    {
                        TemperaturaRecebida?.Invoke(temperatura);
                    }
                }
            }
            catch
            {
                // Erros de leitura parcial de linha ignorados para evitar falha no thread
            }
        }
    }
}