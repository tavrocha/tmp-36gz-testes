using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using Microsoft.Win32;
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
            string[] portasSistema = Array.Empty<string>();

            // 1. Obtém as portas COM direto do sistema (HARDWARE\DEVICEMAP\SERIALCOMM)
            try
            {
                portasSistema = SerialPort.GetPortNames().Distinct().ToArray();
            }
            catch (Exception ex)
            {
                LogGerado?.Invoke($"[ERRO SISTEMA]: Falha ao listar portas seriais: {ex.Message}");
            }

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

            // 2. Tenta buscar descrições amigáveis sem derrubar a listagem se o registro for bloqueado
            var descricoesDispositivos = ObterDescricoesPortasRegistro();

            foreach (var porta in portasSistema)
            {
                bool isEsp32 = false;

                if (descricoesDispositivos.TryGetValue(porta, out string? descricao))
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

        private Dictionary<string, string> ObterDescricoesPortasRegistro()
        {
            var dicionario = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var chaveEnum = OpenSubKeySeguro(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Enum");
                if (chaveEnum == null) return dicionario;

                foreach (var busName in GetSubKeyNamesSeguro(chaveEnum))
                {
                    using var chaveBus = OpenSubKeySeguro(chaveEnum, busName);
                    if (chaveBus == null) continue;

                    foreach (var devName in GetSubKeyNamesSeguro(chaveBus))
                    {
                        using var chaveDev = OpenSubKeySeguro(chaveBus, devName);
                        if (chaveDev == null) continue;

                        foreach (var instName in GetSubKeyNamesSeguro(chaveDev))
                        {
                            using var chaveInst = OpenSubKeySeguro(chaveDev, instName);
                            if (chaveInst == null) continue;

                            using var chaveParam = OpenSubKeySeguro(chaveInst, "Device Parameters");
                            if (chaveParam != null)
                            {
                                string? portName = chaveParam.GetValue("PortName")?.ToString();
                                if (!string.IsNullOrEmpty(portName) && portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                                {
                                    string friendlyName = chaveInst.GetValue("FriendlyName")?.ToString() ?? "";
                                    string deviceDesc = chaveInst.GetValue("DeviceDesc")?.ToString() ?? "";
                                    dicionario[portName] = $"{friendlyName} {deviceDesc}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogGerado?.Invoke($"[AVISO REGISTRO]: {ex.Message}");
            }

            return dicionario;
        }

        // Métodos de segurança para ignorar subchaves sem permissão de leitura de Administrador
        private string[] GetSubKeyNamesSeguro(RegistryKey key)
        {
            try { return key.GetSubKeyNames(); }
            catch { return Array.Empty<string>(); }
        }

        private RegistryKey? OpenSubKeySeguro(RegistryKey key, string subKeyName)
        {
            try { return key.OpenSubKey(subKeyName); }
            catch { return null; }
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
                   descUpper.Contains("SERIAL");
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