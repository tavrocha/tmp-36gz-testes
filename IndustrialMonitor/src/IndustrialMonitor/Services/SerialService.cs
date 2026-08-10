using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using IndustrialMonitor.Models;

namespace IndustrialMonitor.Services
{
    public class SerialService
    {
        private SerialPort? _serialPort;

        public event Action<double>? TemperaturaRecebida;
        public event Action<bool>? StatusConexaoAlterado;
        public event Action<string>? LogGerado;

        public bool IsConectado => _serialPort?.IsOpen ?? false;

        public List<PortaInfo> ObterPortasDisponiveis()
        {
            var listaPortas = new List<PortaInfo>();
            string[] portasSistema = SerialPort.GetPortNames().Distinct().ToArray();

            if (portasSistema.Length == 0)
            {
                listaPortas.Add(new PortaInfo
                {
                    NomePorta = string.Empty,
                    NomeExibicao = "Nenhuma porta encontrada",
                    IsEsp32 = false
                });
                return listaPortas;
            }

            // Tenta obter os nomes amigáveis via WMI
            Dictionary<string, string> dispositivosWmi = new();
            try
            {
                dispositivosWmi = ObterDispositivosComWmiIsolado();
            }
            catch (Exception ex)
            {
                LogGerado?.Invoke($"[AVISO WMI]: Falha ao identificar nomes de dispositivos: {ex.Message}");
            }

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

        // Método isolado do JIT para evitar exceção de montagem caso System.Management não carregue
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Dictionary<string, string> ObterDispositivosComWmiIsolado()
        {
            var dicionario = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

            return dicionario;
        }

        private bool ContemChipEsp32(string descricao)
        {
            if (string.IsNullOrEmpty(descricao)) return false;

            string descUpper = descricao.ToUpper();

            return descUpper.Contains("CP210") ||
                   descUpper.Contains("CH340") ||
                   descUpper.Contains("CH341") ||
                   descUpper.Contains("FT232") ||
                   descUpper.Contains("ESPRESSIF") ||
                   descUpper.Contains("ESP32") ||
                   descUpper.Contains("VID_10C4") ||
                   descUpper.Contains("VID_1A86") ||
                   descUpper.Contains("VID_303A") ||
                   descUpper.Contains("VID_0403");
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
                    RtsEnable = true,
                    ReadTimeout = 2000
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                LogGerado?.Invoke($"[INFO] Porta {porta} aberta a {baudRate} baud.");
                StatusConexaoAlterado?.Invoke(true);
                return (true, "Conectado com sucesso.");
            }
            catch (UnauthorizedAccessException)
            {
                LogGerado?.Invoke($"[ERRO] Porta {porta} ocupada por outro programa.");
                StatusConexaoAlterado?.Invoke(false);
                return (false, $"A porta {porta} está em uso por outro programa (ex: Monitor Serial do Arduino).\nFeche-o e tente novamente.");
            }
            catch (Exception ex)
            {
                LogGerado?.Invoke($"[ERRO] Falha ao abrir {porta}: {ex.Message}");
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
                LogGerado?.Invoke("[INFO] Porta desconectada.");
            }
            StatusConexaoAlterado?.Invoke(false);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                string linha = _serialPort.ReadLine().Trim();

                if (string.IsNullOrEmpty(linha)) return;

                LogGerado?.Invoke($"[RAW RECEBIDO]: \"{linha}\"");

                linha = linha.Replace(',', '.');

                double temperatura = 0;
                bool conversaoSucesso = false;

                if (linha.StartsWith("TEMP=", StringComparison.OrdinalIgnoreCase))
                {
                    string valorTexto = linha.Substring(5).Trim();
                    conversaoSucesso = double.TryParse(
                        valorTexto,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out temperatura
                    );
                }
                else
                {
                    conversaoSucesso = double.TryParse(
                        linha,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out temperatura
                    );
                }

                if (conversaoSucesso)
                {
                    LogGerado?.Invoke($"[PARSER OK]: {temperatura} °C");
                    TemperaturaRecebida?.Invoke(temperatura);
                }
                else
                {
                    LogGerado?.Invoke($"[PARSER FALHA]: Não foi possível converter '{linha}' para número.");
                }
            }
            catch (TimeoutException)
            {
                // Leitura parcial normal
            }
            catch (Exception ex)
            {
                LogGerado?.Invoke($"[ERRO LEITURA]: {ex.Message}");
            }
        }
    }
}