using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using IndustrialMonitor.Models;
using IndustrialMonitor.Services;

namespace IndustrialMonitor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SerialService _serialService;

        private double _temperatura;
        public double Temperatura
        {
            get => _temperatura;
            set
            {
                _temperatura = value;
                OnPropertyChanged(nameof(Temperatura));
            }
        }

        private bool _isConectado;
        public bool IsConectado
        {
            get => _isConectado;
            set
            {
                _isConectado = value;
                OnPropertyChanged(nameof(IsConectado));
                OnPropertyChanged(nameof(StatusConexaoTexto));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string StatusConexaoTexto => IsConectado ? "ESP32 conectado" : "ESP32 desconectado";

        private string _logTexto = "--- AGUARDANDO CONEXÃO ---\n";
        public string LogTexto
        {
            get => _logTexto;
            set
            {
                _logTexto = value;
                OnPropertyChanged(nameof(LogTexto));
            }
        }

        private ObservableCollection<PortaInfo> _portasDisponiveis = new();
        public ObservableCollection<PortaInfo> PortasDisponiveis
        {
            get => _portasDisponiveis;
            set
            {
                _portasDisponiveis = value;
                OnPropertyChanged(nameof(PortasDisponiveis));
            }
        }

        private PortaInfo? _portaSelecionada;
        public PortaInfo? PortaSelecionada
        {
            get => _portaSelecionada;
            set
            {
                _portaSelecionada = value;
                OnPropertyChanged(nameof(PortaSelecionada));
            }
        }

        public ICommand ConectarCommand { get; }
        public ICommand DesconectarCommand { get; }
        public ICommand AtualizarPortasCommand { get; }

        public MainViewModel()
        {
            _serialService = new SerialService();

            _serialService.TemperaturaRecebida += OnTemperaturaRecebida;
            _serialService.StatusConexaoAlterado += OnStatusConexaoAlterado;
            _serialService.LogGerado += OnLogGerado;

            ConectarCommand = new RelayCommand(_ => Conectar(), _ => !IsConectado && PortaSelecionada != null && !string.IsNullOrEmpty(PortaSelecionada.NomePorta));
            DesconectarCommand = new RelayCommand(_ => Desconectar(), _ => IsConectado);
            AtualizarPortasCommand = new RelayCommand(_ => CarregarPortas());

            CarregarPortas();
        }

        private void OnLogGerado(string mensagem)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                string hora = DateTime.Now.ToString("HH:mm:ss");
                LogTexto += $"[{hora}] {mensagem}\n";
            });
        }

        private void OnTemperaturaRecebida(double temperatura)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Temperatura = temperatura;
            });
        }

        private void OnStatusConexaoAlterado(bool conectado)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsConectado = conectado;
            });
        }

        private void CarregarPortas()
        {
            var portas = _serialService.ObterPortasDisponiveis();
            PortasDisponiveis.Clear();

            foreach (var porta in portas)
            {
                PortasDisponiveis.Add(porta);
            }

            PortaSelecionada = PortasDisponiveis.FirstOrDefault(p => p.IsEsp32) ?? PortasDisponiveis.FirstOrDefault();
        }

        private void Conectar()
        {
            if (PortaSelecionada != null && !string.IsNullOrEmpty(PortaSelecionada.NomePorta))
            {
                var (sucesso, mensagem) = _serialService.Conectar(PortaSelecionada.NomePorta);
                if (!sucesso)
                {
                    MessageBox.Show(mensagem, "Aviso de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Desconectar()
        {
            _serialService.Disconectar();
        }
    }
}