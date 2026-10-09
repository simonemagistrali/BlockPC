using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BlockPC.Shared;

namespace BlockPC.UI
{
    public partial class MainWindow : Window
    {
        private readonly IpcClient _ipcClient = new();
        private BlockingPolicy? _currentPolicy;
        private bool _isBlockActive;

        public MainWindow()
        {
            InitializeComponent();
            SetupDaysCheckboxes();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshStateAsync();
        }

        private void SetupDaysCheckboxes()
        {
            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                var cb = new CheckBox
                {
                    Content = day.ToString(),
                    Tag = day,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                PanelDays.Children.Add(cb);
            }
        }

        private async System.Threading.Tasks.Task RefreshStateAsync()
        {
            var state = await _ipcClient.GetStateAsync();
            if (state == null || state.Policy == null)
            {
                StatusText.Text = "ERRORE DI CONNESSIONE: Il BlockerService è in esecuzione?";
                StatusBanner.Background = Brushes.LightCoral;
                StatusText.Foreground = Brushes.DarkRed;
                return;
            }

            _currentPolicy = state.Policy;
            _isBlockActive = state.IsActive;

            // Aggiorna UI
            if (_isBlockActive)
            {
                StatusText.Text = "BLOCCO ATTIVO - Modalità Append-Only (Puoi solo aggiungere restrizioni)";
                StatusBanner.Background = Brushes.Crimson;
                StatusText.Foreground = Brushes.White;
            }
            else
            {
                StatusText.Text = "BLOCCO IN PAUSA - Puoi modificare liberamente le policy";
                StatusBanner.Background = Brushes.LightGreen;
                StatusText.Foreground = Brushes.DarkGreen;
            }

            // Popola Liste
            ListProcesses.ItemsSource = null;
            ListProcesses.ItemsSource = _currentPolicy.BlockedProcesses;

            ListDomains.ItemsSource = null;
            ListDomains.ItemsSource = _currentPolicy.BlockedDomains;

            // Popola Scheduling
            foreach (CheckBox cb in PanelDays.Children)
            {
                var day = (DayOfWeek)cb.Tag;
                cb.IsChecked = _currentPolicy.ActiveDays.Contains(day);
                
                // Append-Only rule: se il blocco è attivo e il giorno è checkato, non si può disattivare
                if (_isBlockActive && cb.IsChecked == true)
                {
                    cb.IsEnabled = false;
                }
                else
                {
                    cb.IsEnabled = true;
                }
            }

            TxtStartTime.Text = _currentPolicy.StartTime.ToString(@"hh\:mm");
            TxtEndTime.Text = _currentPolicy.EndTime.ToString(@"hh\:mm");

            // Disabilita rimozione se blocco attivo
            BtnRemoveProcess.IsEnabled = !_isBlockActive;
            BtnRemoveDomain.IsEnabled = !_isBlockActive;
            TxtStartTime.IsEnabled = !_isBlockActive;
            TxtEndTime.IsEnabled = !_isBlockActive;
        }

        private void BtnAddProcess_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPolicy == null || string.IsNullOrWhiteSpace(TxtNewProcess.Text)) return;
            var proc = TxtNewProcess.Text.Trim();
            if (!_currentPolicy.BlockedProcesses.Contains(proc))
            {
                _currentPolicy.BlockedProcesses.Add(proc);
                ListProcesses.ItemsSource = null;
                ListProcesses.ItemsSource = _currentPolicy.BlockedProcesses;
            }
            TxtNewProcess.Text = "";
        }

        private void BtnRemoveProcess_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPolicy == null || _isBlockActive) return;
            if (ListProcesses.SelectedItem is string sel)
            {
                _currentPolicy.BlockedProcesses.Remove(sel);
                ListProcesses.ItemsSource = null;
                ListProcesses.ItemsSource = _currentPolicy.BlockedProcesses;
            }
        }

        private void BtnAddDomain_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPolicy == null || string.IsNullOrWhiteSpace(TxtNewDomain.Text)) return;
            var dom = TxtNewDomain.Text.Trim();
            if (!_currentPolicy.BlockedDomains.Contains(dom))
            {
                _currentPolicy.BlockedDomains.Add(dom);
                ListDomains.ItemsSource = null;
                ListDomains.ItemsSource = _currentPolicy.BlockedDomains;
            }
            TxtNewDomain.Text = "";
        }

        private void BtnRemoveDomain_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPolicy == null || _isBlockActive) return;
            if (ListDomains.SelectedItem is string sel)
            {
                _currentPolicy.BlockedDomains.Remove(sel);
                ListDomains.ItemsSource = null;
                ListDomains.ItemsSource = _currentPolicy.BlockedDomains;
            }
        }

        private async void BtnQuickBlock_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPolicy == null) return;
            int hours = int.Parse(((ComboBoxItem)QuickBlockHours.SelectedItem).Content.ToString()!);
            
            DateTime newEndTime = DateTime.Now.AddHours(hours);
            
            // Append-only per QuickBlock
            if (_currentPolicy.QuickBlockEndTime.HasValue && _currentPolicy.QuickBlockEndTime.Value > newEndTime)
            {
                MessageBox.Show("Il blocco rapido attuale termina più tardi. Puoi solo aumentare il tempo.", "Avviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentPolicy.QuickBlockEndTime = newEndTime;
            
            await SavePolicyAsync();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            await SavePolicyAsync();
        }

        private async System.Threading.Tasks.Task SavePolicyAsync()
        {
            if (_currentPolicy == null) return;

            // Legge i giorni
            _currentPolicy.ActiveDays.Clear();
            foreach (CheckBox cb in PanelDays.Children)
            {
                if (cb.IsChecked == true)
                {
                    _currentPolicy.ActiveDays.Add((DayOfWeek)cb.Tag);
                }
            }

            if (TimeSpan.TryParse(TxtStartTime.Text, out var st))
                _currentPolicy.StartTime = st;
            
            if (TimeSpan.TryParse(TxtEndTime.Text, out var et))
                _currentPolicy.EndTime = et;

            var result = await _ipcClient.UpdatePolicyAsync(_currentPolicy);
            if (result == "OK")
            {
                MessageBox.Show("Policy aggiornata con successo!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshStateAsync();
            }
            else
            {
                MessageBox.Show(result, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                // Ricarica lo stato corretto se c'è stato un rifiuto dal server
                await RefreshStateAsync();
            }
        }
    }
}