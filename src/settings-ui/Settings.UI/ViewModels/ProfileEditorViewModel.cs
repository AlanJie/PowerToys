// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Profile Editor Dialog
    /// </summary>
    public class ProfileEditorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly int _profileId;
        private readonly ObservableCollection<MonitorSelectionItem> _monitors;
        private string _profileName = string.Empty;

        public ProfileEditorViewModel(
            ObservableCollection<MonitorInfo> availableMonitors,
            string defaultName = "",
            int profileId = 0)
        {
            _profileId = profileId;
            _profileName = defaultName;
            _monitors = new ObservableCollection<MonitorSelectionItem>();

            // Set TotalMonitorCount for DisplayName to show monitor numbers when multiple monitors exist
            int totalCount = availableMonitors.Count;
            foreach (var monitor in availableMonitors)
            {
                monitor.TotalMonitorCount = totalCount;
            }

            // Initialize monitor selection items
            foreach (var monitor in availableMonitors)
            {
                var item = new MonitorSelectionItem(monitor)
                {
                    SuppressAutoSelection = true,
                    IsSelected = false,
                    Brightness = monitor.CurrentBrightness,
                    Contrast = 50, // Default value (MonitorInfo doesn't store contrast)
                    Volume = 50, // Default value (MonitorInfo doesn't store volume)
                    ColorTemperature = monitor.ColorTemperatureVcp,
                };

                item.SuppressAutoSelection = false;

                // Subscribe to selection and checkbox changes
                item.PropertyChanged += OnMonitorItemPropertyChanged;

                _monitors.Add(item);
            }
        }

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public ObservableCollection<MonitorSelectionItem> Monitors => _monitors;

        public bool HasSelectedMonitors => _monitors?.Any(m => m.IsSelected) ?? false;

        public bool HasValidSettings => _monitors != null &&
            _monitors.Any(m => m.IsSelected) &&
            _monitors.Where(m => m.IsSelected).All(m =>
                (m.IncludeBrightness ||
                    (m.IncludeContrast && m.SupportsContrast) ||
                    (m.IncludeVolume && m.SupportsVolume) ||
                    (m.IncludeColorTemperature && m.HasValidColorTemperature)) &&
                (!m.IncludeColorTemperature || !m.SupportsColorTemperature || m.HasValidColorTemperature));

        public bool CanSave => !string.IsNullOrWhiteSpace(_profileName) && HasSelectedMonitors && HasValidSettings;

        public PowerDisplayProfile CreateProfile()
        {
            var settings = _monitors
                .Where(m => m.IsSelected)
                .Select(m => new ProfileMonitorSetting(
                    m.Monitor.Id, // Monitor Id (unique identifier)
                    m.IncludeBrightness ? (int?)m.Brightness : null,
                    m.IncludeColorTemperature && m.HasValidColorTemperature ? m.ColorTemperature : null,
                    m.IncludeContrast && m.SupportsContrast ? (int?)m.Contrast : null,
                    m.IncludeVolume && m.SupportsVolume ? (int?)m.Volume : null))
                .ToList();

            return new PowerDisplayProfile(_profileName, settings) { Id = _profileId };
        }

        /// <summary>
        /// Pre-fill a new editor with the existing profile's monitor settings.
        /// </summary>
        public void PreFillProfile(PowerDisplayProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            ProfileName = profile.Name;

            foreach (var monitorSetting in profile.MonitorSettings)
            {
                var monitorItem = _monitors.FirstOrDefault(m => MonitorIdComparer.Equal(m.Monitor.Id, monitorSetting.MonitorId));
                if (monitorItem != null)
                {
                    monitorItem.IsSelected = true;

                    if (monitorSetting.Brightness.HasValue)
                    {
                        monitorItem.IncludeBrightness = true;
                        monitorItem.Brightness = monitorSetting.Brightness.Value;
                    }

                    if (monitorSetting.ColorTemperatureVcp.HasValue)
                    {
                        monitorItem.IncludeColorTemperature = true;
                        monitorItem.ColorTemperature = monitorSetting.ColorTemperatureVcp.Value;
                    }

                    if (monitorSetting.Contrast.HasValue)
                    {
                        monitorItem.IncludeContrast = true;
                        monitorItem.Contrast = monitorSetting.Contrast.Value;
                    }

                    if (monitorSetting.Volume.HasValue)
                    {
                        monitorItem.IncludeVolume = true;
                        monitorItem.Volume = monitorSetting.Volume.Value;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var monitor in _monitors)
                {
                    monitor.PropertyChanged -= OnMonitorItemPropertyChanged;
                    monitor.Dispose();
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handle property changes from monitor selection items.
        /// Centralizes validation state updates to avoid duplication.
        /// </summary>
        private void OnMonitorItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update selection-dependent properties
            if (e.PropertyName == nameof(MonitorSelectionItem.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelectedMonitors));
            }

            // Update validation state for relevant property changes
            if (e.PropertyName == nameof(MonitorSelectionItem.IsSelected) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeBrightness) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeContrast) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeVolume) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeColorTemperature) ||
                e.PropertyName == nameof(MonitorSelectionItem.SupportsContrast) ||
                e.PropertyName == nameof(MonitorSelectionItem.SupportsVolume) ||
                e.PropertyName == nameof(MonitorSelectionItem.HasValidColorTemperature))
            {
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(HasValidSettings));
            }
        }
    }
}
