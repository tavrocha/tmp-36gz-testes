using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using IndustrialMonitor.Models;

namespace IndustrialMonitor.Services
{
    public class SerialService
    {
        private SerialPort? _serialPort;

        public event Action<double>? TemperaturaRecebida;
        public event Action<bool>? StatusConexaoAlterado;

        public bool IsConectado => _serialPort?.IsOpen ?? false;

        public List<PortaInfo> ObterPortasDisponiveis()
        {
            var listaPortas = new List<PortaInfo>();
            string[] portasSistema = SerialPort.GetPortNames().Distinct().ToArray();

            // Se não houver nenhuma porta serial física/virtual conectada no PC
            if (portasSistema.Length == 0)
            {
                listaPortas.Add(new PortaInfo
                {
                    NomePorta = string.Empty,
                    NomeExibicao = "Porta vazia",
                    IsEsp32 = false
                });
                return listaPortas;
            }

            var dispositivosWmi = ObterDispositivosComWmi();

            foreach (var porta in portasSistema)
            {
                bool isEsp32 = false;

                if (dispositivosWmi.TryGetValue(porta, out string? descricao))
                {
                    isEsp32 = ContemChipEsp32(descricao);
                }

                string exibicao = isEsp32 ? $"{porta} - ESP-32" : porta;

                listaPortas.Add(new PortaInfo
                {
                    NomePorta = porta,
                    NomeExibicao = exibicao,
                    IsEsp32 = isEsp32
                });
            }

            return listaPortas;
        }

        private Dictionary<string, string> ObterDispositivosComWmi()
        {
            var dicionario = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption, PNPDeviceID, Description FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");
                using var collection = searcher.Get();

                foreach (var obj in collection)
                {
                    string caption = obj["Caption"]?.ToString() ?? "";
                    string pnpId = obj["PNPDeviceID"]?.ToString() ?? "";
                    string description = obj["Description"]?.ToString() ?? "";
                    string infoCompleta = $"{caption} {pnpId} {description}";

                    int inicio = caption.IndexOf("(COM", StringComparison.OrdinalIgnoreCase);
                    if (inicio != -1)
                    {
                        int fim = caption.IndexOf(")", inicio);
                        if (fim != -1)
                        {
                            string porta = caption.Substring(inicio + 1, fim - inicio - 1);
                            dicionario[porta] = infoCompleta;
                        }
                    }
                }
            }
            catch
            {
                // Ignora exceções e permite execução degradada
            }

            return dicionario;
        }

        private bool ContemChipEsp32(string descricao)
        {
            if (string.IsNullOrEmpty(descricao)) return false;

            string descUpper = descricao.ToUpper();

            // Identificadores de Hardware (VID/PID) e Nomes de Controladores USB-Serial comuns em ESP32
            return descUpper.Contains("CP210") ||     // CP2102 / CP2104 (Silicon Labs)
                   descUpper.Contains("CH340") ||     // CH340G / CH340C (WCH)
                   descUpper.Contains("CH341") ||
                   descUpper.Contains("FT232") ||     // FTDI
                   descUpper.Contains("ESPRESSIF") || // USB CDC Nativo do ESP32
                   descUpper.Contains("ESP32") ||
                   descUpper.Contains("VID_10C4") || // Silicon Labs VID
                   descUpper.Contains("VID_1A86") || // WCH VID
                   descUpper.Contains("VID_303A") || // Espressif VID
                   descUpper.Contains("VID_0403");   // FTDI VID
        }

                public (bool Sucesso, string Mensagem) Conectar(string porta, int baudRate = 115200)
        {
            if (string.IsNullOrEmpty(porta)) 
                return (false, "Nenhuma porta válida selecionada.");

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
                return (true, "Conectado com sucesso.");
            }
            catch (UnauthorizedAccessException)
            {
                StatusConexaoAlterado?.Invoke(false);
                return (false, $"A porta {porta} está em uso por outro programa (ex: Monitor Serial da IDE do Arduino).\nFeche-o e tente novamente.");
            }
            catch (Exception ex)
            {
                StatusConexaoAlterado?.Invoke(false);
                return (false, $"Erro ao abrir a porta {porta}: {ex.Message}");
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
                // Erros de leitura parcial de linha ignorados
            }
        }
    }
}