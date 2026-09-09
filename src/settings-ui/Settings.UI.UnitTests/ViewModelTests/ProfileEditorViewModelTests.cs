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

            // Match the order used to pre-fill an existing profile in the dialog.
            item.IsSelected = true;
            item.IncludeColorTemperature = true;
            item.ColorTemperature = savedValue;

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
            monitor.SupportsColorTemperature = false;

            Assert.AreEqual(0, itemChanges);
            Assert.AreEqual(0, editorChanges);
        }

        private static ProfileEditorViewModel CreateViewModel(MonitorInfo monitor)
        {
            return new ProfileEditorViewModel(new ObservableCollection<MonitorInfo> { monitor }, "Profile");
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
