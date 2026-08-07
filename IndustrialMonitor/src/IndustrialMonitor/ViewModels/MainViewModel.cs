using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IndustrialMonitor.Services;

namespace IndustrialMonitor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SerialService _serialService;
        private double _temperaturaAtual;
        private string _statusTexto = "🔴 ESP32 desconectado";
        private bool _isConectado;
        private string? _portaSelecionada;

        public ObservableCollection<string> PortasDisponiveis { get; } = new();

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

        public string? PortaSelecionada
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

            ConectarCommand = new RelayCommand(_ => Conectar(), _ => !IsConectado && !string.IsNullOrEmpty(PortaSelecionada));
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

            if (PortasDisponiveis.Count > 0)
            {
                PortaSelecionada = PortasDisponiveis[0];
            }
        }

        private void Conectar()
        {
            if (PortaSelecionada != null)
            {
                _serialService.Conectar(PortaSelecionada);
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
                    ? $"🟢 ESP32 conectado ({PortaSelecionada})" 
                    : "🔴 ESP32 desconectado";
            });
        }
    }
}