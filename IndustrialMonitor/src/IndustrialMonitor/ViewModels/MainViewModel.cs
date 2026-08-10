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
        private double _temperaturaAtual;
        private string _statusTexto = "🔴 ESP32 desconectado";
        private bool _isConectado;
        private PortaInfo? _portaSelecionada;

        public ObservableCollection<PortaInfo> PortasDisponiveis { get; } = new();

        public double TemperaturaAtual
        {
            get => _temperaturaAtual;
            set => SetProperty(ref _temperaturaAtual, value);
        }

        public string StatusTexto
        {
            get => _statusTexto;
            set => SetProperty(ref _statusTexto, value);
        }

        public bool IsConectado
        {
            get => _isConectado;
            set
            {
                if (SetProperty(ref _isConectado, value))
                {
                    ((RelayCommand)ConectarCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DesconectarCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public PortaInfo? PortaSelecionada
        {
            get => _portaSelecionada;
            set
            {
                if (SetProperty(ref _portaSelecionada, value))
                {
                    ((RelayCommand)ConectarCommand).RaiseCanExecuteChanged();
                }
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

            ConectarCommand = new RelayCommand(_ => Conectar(), _ => !IsConectado && PortaSelecionada != null && !string.IsNullOrEmpty(PortaSelecionada.NomePorta));
            DesconectarCommand = new RelayCommand(_ => Desconectar(), _ => IsConectado);
            AtualizarPortasCommand = new RelayCommand(_ => CarregarPortas());

            CarregarPortas();
        }

        private void CarregarPortas()
        {
            PortasDisponiveis.Clear();
            var portas = _serialService.ObterPortasDisponiveis();

            foreach (var porta in portas)
            {
                PortasDisponiveis.Add(porta);
            }

            var portaEsp32 = PortasDisponiveis.FirstOrDefault(p => p.IsEsp32);
            if (portaEsp32 != null)
            {
                PortaSelecionada = portaEsp32;
            }
            else if (PortasDisponiveis.Count > 0)
            {
                PortaSelecionada = PortasDisponiveis[0];
            }
        }

        private void Conectar()
        {
            if (PortaSelecionada != null && !string.IsNullOrEmpty(PortaSelecionada.NomePorta))
            {
                _serialService.Conectar(PortaSelecionada.NomePorta);
            }
        }

        private void Desconectar()
        {
            _serialService.Disconectar();
        }

        private void OnTemperaturaRecebida(double temp)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TemperaturaAtual = temp;
            });
        }

        private void OnStatusConexaoAlterado(bool conectado)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConectado = conectado;
                StatusTexto = conectado 
                    ? $"🟢 ESP32 conectado ({PortaSelecionada?.NomeExibicao})" 
                    : "🔴 ESP32 desconectado";
            });
        }
    }
}