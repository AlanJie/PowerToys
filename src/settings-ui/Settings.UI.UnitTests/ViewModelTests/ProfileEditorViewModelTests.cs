// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests
{
    [TestClass]
    public class ProfileEditorViewModelTests
    {
        [TestMethod]
        public void CreateProfile_DefaultProfileId_ReturnsZero()
        {
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "New profile");

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(0, profile.Id);
        }

        [TestMethod]
        public void CreateProfile_ExistingProfileId_PreservesId()
        {
            const int profileId = 42;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo>(),
                "Existing profile",
                profileId);

            var profile = viewModel.CreateProfile();

            Assert.AreEqual(profileId, profile.Id);
        }

        [TestMethod]
        public void ColorTemperature_BlockedCurrentValueRequiresAnAllowedSelection()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeColorTemperature = true;

            Assert.AreEqual(0x08, item.ColorPresetsForDisplay.Single().VcpValue);
            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);

            item.ColorTemperature = 0x08;

            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.IsTrue(viewModel.CanSave);
            Assert.AreEqual((int?)0x08, viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_InvalidIncludedColorTemperatureBlocksOtherSettingsUntilUnchecked()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;

            Assert.IsFalse(viewModel.CanSave);
            var settings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, settings.Brightness);
            Assert.IsNull(settings.ColorTemperatureVcp);

            item.IncludeColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void ColorTemperature_AllPresetsBlockedCannotBeIncluded()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05, 0x08);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeColorTemperature = true;

            Assert.AreEqual(0, item.ColorPresetsForDisplay.Count);
            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow(0x05)]
        [DataRow(0x0B)]
        public void ColorTemperature_PrefillingAnUnavailableValueClearsThePreviousSelection(int savedValue)
        {
            var monitor = CreateMonitor();
            monitor.ColorTemperatureVcp = 0x08;
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            Assert.AreEqual((int?)0x08, item.ColorTemperature);

            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, colorTemperatureVcp: savedValue) }));

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void ColorTemperature_ClearingSelectionDoesNotIncludeTheSetting()
        {
            using var viewModel = CreateViewModel(CreateMonitor());
            var item = viewModel.Monitors.Single();
            Assert.IsFalse(item.IncludeColorTemperature);

            item.ColorTemperature = null;

            Assert.IsNull(item.ColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(item.IncludeColorTemperature);

            item.ColorTemperature = 0x08;

            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void ColorPresets_RefreshPreservesSelectionAndInclusion(bool renamePreset, bool includeColorTemperature)
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.SuppressAutoSelection = true;
            item.ColorTemperature = 0x08;
            item.SuppressAutoSelection = false;
            item.IncludeColorTemperature = includeColorTemperature;
            var presetChanges = 0;
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MonitorSelectionItem.ColorPresetsForDisplay))
                {
                    presetChanges++;

                    // Replacing ComboBox.ItemsSource can synchronously clear SelectedValue.
                    item.ColorTemperature = null;
                }
            };

            if (renamePreset)
            {
                monitor.VcpCodesFormatted = CreateColorCapabilities("Renamed preset");
            }
            else
            {
                monitor.DisabledVcpValues = new List<VcpValueBlock>();
            }

            Assert.IsTrue(presetChanges > 0);
            Assert.AreEqual((int?)0x08, item.ColorTemperature);
            Assert.IsTrue(item.HasValidColorTemperature);
            Assert.AreEqual(includeColorTemperature, item.IncludeColorTemperature);
            if (renamePreset)
            {
                Assert.AreEqual("Renamed preset", item.ColorPresetsForDisplay.Single(preset => preset.VcpValue == 0x08).DisplayName);
            }
        }

        [TestMethod]
        public void ColorPresets_BlockingTheSelectedValueClearsSelectionAndNotifiesValidation()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.ColorTemperature = 0x08;
            Assert.IsTrue(viewModel.CanSave);
            var canSaveChanged = false;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanged = true;
                }
            };

            monitor.DisabledVcpValues = CreateColorRestrictions(0x08);

            Assert.IsNull(item.ColorTemperature);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsFalse(viewModel.CanSave);
            Assert.IsTrue(canSaveChanged);
            Assert.IsNull(viewModel.CreateProfile().MonitorSettings.Single().ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_UnsupportedColorTemperatureDoesNotBlockBrightness()
        {
            var monitor = CreateMonitor();
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            item.IsSelected = true;
            item.IncludeBrightness = true;
            item.IncludeColorTemperature = true;
            item.ColorTemperature = 0x05;

            Assert.IsFalse(item.HasValidColorTemperature);
            Assert.IsTrue(viewModel.CanSave);
            var settings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual((int?)monitor.CurrentBrightness, settings.Brightness);
            Assert.IsNull(settings.ColorTemperatureVcp);
        }

        [TestMethod]
        public void PreFillProfile_CaseInsensitiveMonitorIdRestoresEverySettingOnMatchingInstance()
        {
            var monitor = CreateMonitor();
            monitor.Id = @"\\?\DISPLAY#TEST001#FIRST";
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            var otherMonitor = CreateMonitor();
            otherMonitor.Id = @"\\?\DISPLAY#TEST001#SECOND";
            otherMonitor.CurrentBrightness = 90;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo> { otherMonitor, monitor },
                "New profile");

            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting>
                {
                    new(monitor.Id.ToLowerInvariant(), brightness: 25, colorTemperatureVcp: 0x08, contrast: 63, volume: 41),
                }));

            var item = viewModel.Monitors[1];
            Assert.AreSame(monitor, item.Monitor);
            Assert.AreEqual("Existing profile", viewModel.ProfileName);
            Assert.IsTrue(item.IsSelected);
            Assert.IsTrue(item.IncludeBrightness);
            Assert.IsTrue(item.IncludeContrast);
            Assert.IsTrue(item.IncludeVolume);
            Assert.IsTrue(item.IncludeColorTemperature);
            Assert.AreEqual(25, item.Brightness);
            Assert.AreEqual(63, item.Contrast);
            Assert.AreEqual(41, item.Volume);
            Assert.AreEqual((int?)0x08, item.ColorTemperature);
            Assert.IsTrue(viewModel.CanSave);

            var otherItem = viewModel.Monitors[0];
            Assert.AreSame(otherMonitor, otherItem.Monitor);
            Assert.IsFalse(otherItem.IsSelected);
            Assert.IsFalse(otherItem.IncludeBrightness);
            Assert.IsFalse(otherItem.IncludeContrast);
            Assert.IsFalse(otherItem.IncludeVolume);
            Assert.IsFalse(otherItem.IncludeColorTemperature);
            Assert.AreEqual(90, otherItem.Brightness);
            Assert.AreEqual((int?)0x05, otherItem.ColorTemperature);

            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual(monitor.Id, savedSettings.MonitorId);
            Assert.AreEqual((int?)25, savedSettings.Brightness);
            Assert.AreEqual((int?)63, savedSettings.Contrast);
            Assert.AreEqual((int?)41, savedSettings.Volume);
            Assert.AreEqual((int?)0x08, savedSettings.ColorTemperatureVcp);
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        [DataRow(nameof(ProfileMonitorSetting.ColorTemperatureVcp))]
        public void CanSave_PrefillingOnlyAnUnsupportedSettingDoesNotAllowEmptyMonitorSettings(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = false;
            monitor.SupportsVolume = false;
            monitor.SupportsColorTemperature = false;
            using var viewModel = CreateViewModel(monitor);

            viewModel.PreFillProfile(CreateProfileWithOptionalSetting(monitor.Id, settingName));

            var item = viewModel.Monitors.Single();
            Assert.IsTrue(item.IsSelected);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Contrast), item.IncludeContrast);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.Volume), item.IncludeVolume);
            Assert.AreEqual(settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp), item.IncludeColorTemperature);
            Assert.IsFalse(viewModel.HasValidSettings);
            Assert.IsFalse(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.IsNull(savedSettings.Brightness);
            Assert.IsNull(savedSettings.Contrast);
            Assert.IsNull(savedSettings.Volume);
            Assert.IsNull(savedSettings.ColorTemperatureVcp);
        }

        [TestMethod]
        public void CanSave_EverySelectedMonitorNeedsAPersistedSetting()
        {
            var monitor = CreateMonitor();
            var unsupportedMonitor = CreateMonitor();
            unsupportedMonitor.Id = "DISPLAY#TEST001#2";
            unsupportedMonitor.SupportsContrast = false;
            using var viewModel = new ProfileEditorViewModel(
                new ObservableCollection<MonitorInfo> { monitor, unsupportedMonitor },
                "Profile");
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting>
                {
                    new(monitor.Id, brightness: 25),
                    new(unsupportedMonitor.Id, contrast: 63),
                }));

            Assert.IsTrue(viewModel.Monitors.All(item => item.IsSelected));
            Assert.IsFalse(viewModel.CanSave);

            viewModel.Monitors[1].IsSelected = false;

            Assert.IsTrue(viewModel.CanSave);
            var savedSettings = viewModel.CreateProfile().MonitorSettings.Single();
            Assert.AreEqual(monitor.Id, savedSettings.MonitorId);
            Assert.AreEqual((int?)25, savedSettings.Brightness);
        }

        [TestMethod]
        [DataRow(nameof(ProfileMonitorSetting.Contrast))]
        [DataRow(nameof(ProfileMonitorSetting.Volume))]
        public void CanSave_OptionalSettingSupportChangesNotifyValidation(string settingName)
        {
            var monitor = CreateMonitor();
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(CreateProfileWithOptionalSetting(monitor.Id, settingName));
            Assert.IsTrue(viewModel.CanSave);
            var canSaveChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanges++;
                }
            };

            if (settingName == nameof(ProfileMonitorSetting.Contrast))
            {
                monitor.SupportsContrast = false;
            }
            else
            {
                monitor.SupportsVolume = false;
            }

            Assert.IsFalse(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
            canSaveChanges = 0;

            if (settingName == nameof(ProfileMonitorSetting.Contrast))
            {
                monitor.SupportsContrast = true;
            }
            else
            {
                monitor.SupportsVolume = true;
            }

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
        }

        [TestMethod]
        public void CanSave_ColorTemperatureSupportChangesNotifyValidation()
        {
            var monitor = CreateMonitor();
            monitor.DisabledVcpValues = CreateColorRestrictions(0x05, 0x08);
            using var viewModel = CreateViewModel(monitor);
            viewModel.PreFillProfile(new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting> { new(monitor.Id, brightness: 25, colorTemperatureVcp: 0x08) }));
            Assert.IsFalse(viewModel.CanSave);
            var canSaveChanges = 0;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProfileEditorViewModel.CanSave))
                {
                    canSaveChanges++;
                }
            };

            monitor.SupportsColorTemperature = false;

            Assert.IsTrue(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
            canSaveChanges = 0;

            monitor.SupportsColorTemperature = true;

            Assert.IsFalse(viewModel.CanSave);
            Assert.IsTrue(canSaveChanges > 0);
        }

        [TestMethod]
        public void Dispose_MonitorChangesDoNotNotifyTheEditor()
        {
            var monitor = CreateMonitor();
            using var viewModel = CreateViewModel(monitor);
            var item = viewModel.Monitors.Single();
            viewModel.Dispose();
            var itemChanges = 0;
            var editorChanges = 0;
            item.PropertyChanged += (_, _) => itemChanges++;
            viewModel.PropertyChanged += (_, _) => editorChanges++;

            monitor.DisabledVcpValues = CreateColorRestrictions(0x05);
            monitor.VcpCodesFormatted = CreateColorCapabilities("Renamed preset");
            monitor.SupportsContrast = true;
            monitor.SupportsVolume = true;
            monitor.SupportsColorTemperature = false;

            Assert.AreEqual(0, itemChanges);
            Assert.AreEqual(0, editorChanges);
        }

        private static ProfileEditorViewModel CreateViewModel(MonitorInfo monitor)
        {
            return new ProfileEditorViewModel(new ObservableCollection<MonitorInfo> { monitor }, "Profile");
        }

        private static PowerDisplayProfile CreateProfileWithOptionalSetting(string monitorId, string settingName)
        {
            return new PowerDisplayProfile(
                "Existing profile",
                new List<ProfileMonitorSetting>
                {
                    new(
                        monitorId,
                        colorTemperatureVcp: settingName == nameof(ProfileMonitorSetting.ColorTemperatureVcp) ? 0x08 : null,
                        contrast: settingName == nameof(ProfileMonitorSetting.Contrast) ? 63 : null,
                        volume: settingName == nameof(ProfileMonitorSetting.Volume) ? 41 : null),
                });
        }

        private static MonitorInfo CreateMonitor()
        {
            return new MonitorInfo
            {
                Id = "DISPLAY#TEST001#1",
                Name = "Monitor",
                CurrentBrightness = 75,
                ColorTemperatureVcp = 0x05,
                SupportsColorTemperature = true,
                VcpCodesFormatted = CreateColorCapabilities(),
            };
        }

        private static List<VcpCodeDisplayInfo> CreateColorCapabilities(string secondPresetName = "9300 K")
        {
            return new()
            {
                new()
                {
                    Code = "0x14",
                    Title = "Color preset (0x14)",
                    HasValues = true,
                    ValueList = new()
                    {
                        new() { Value = "0x05", Name = "6500 K" },
                        new() { Value = "0x08", Name = secondPresetName },
                    },
                },
            };
        }

        private static List<VcpValueBlock> CreateColorRestrictions(params int[] values)
        {
            return new() { new() { VcpCode = 0x14, Values = values.ToList() } };
        }
    }
}
