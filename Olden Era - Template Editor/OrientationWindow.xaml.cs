using System.Linq;
using System.Windows;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor
{
    public partial class OrientationWindow : Window
    {
        private readonly Variant _variant;

        public OrientationWindow(Variant variant)
        {
            InitializeComponent();
            _variant = variant;

            // Populate zeroAngleZone from zone names
            if (variant.Zones is not null)
            {
                foreach (var z in variant.Zones.Where(z => !string.IsNullOrWhiteSpace(z.Name)))
                    CmbZeroAngleZone.Items.Add(z.Name);
            }

            LoadCurrentValues();
        }

        private void LoadCurrentValues()
        {
            var o = _variant.Orientation;
            if (o is null)
            {
                CmbMode.SelectedIndex = 2; // "(не задано)"
            }
            else
            {
                CmbMode.SelectedIndex = o.Mode switch
                {
                    "MinimalBoundingSquare" => 0,
                    "BoundingCircle" => 1,
                    _ => 2,
                };

                if (!string.IsNullOrEmpty(o.ZeroAngleZone) && CmbZeroAngleZone.Items.Contains(o.ZeroAngleZone))
                    CmbZeroAngleZone.SelectedItem = o.ZeroAngleZone;
                else if (o.ZeroAngleZone is not null)
                    CmbZeroAngleZone.Text = o.ZeroAngleZone;

                TxtBaseAngleMin.Text = o.BaseAngleMin?.ToString() ?? "45";
                TxtBaseAngleMax.Text = o.BaseAngleMax?.ToString() ?? "45";
                TxtRandomAngleAmplitude.Text = o.RandomAngleAmplitude?.ToString() ?? "360";
                TxtRandomAngleStep.Text = o.RandomAngleStep?.ToString() ?? "90";
            }

            var b = _variant.Border;
            if (b is null)
            {
                TxtCornerRadius.Text = "0.0";
                TxtObstaclesWidth.Text = "3";
                TxtObstaclesNoiseAmp.Text = "1";
                TxtObstaclesNoiseFreq.Text = "12";
                TxtWaterWidth.Text = "0";
                TxtWaterNoiseAmp.Text = "1";
                TxtWaterNoiseFreq.Text = "12";
                TxtWaterType.Text = "water grass";
            }
            else
            {
                TxtCornerRadius.Text = b.CornerRadius?.ToString() ?? "0.0";
                TxtObstaclesWidth.Text = b.ObstaclesWidth?.ToString() ?? "3";
                if (b.ObstaclesNoise is { Count: > 0 })
                {
                    TxtObstaclesNoiseAmp.Text = b.ObstaclesNoise[0].Amp.ToString();
                    TxtObstaclesNoiseFreq.Text = b.ObstaclesNoise[0].Freq.ToString();
                }
                TxtWaterWidth.Text = b.WaterWidth?.ToString() ?? "0";
                if (b.WaterNoise is { Count: > 0 })
                {
                    TxtWaterNoiseAmp.Text = b.WaterNoise[0].Amp.ToString();
                    TxtWaterNoiseFreq.Text = b.WaterNoise[0].Freq.ToString();
                }
                TxtWaterType.Text = b.WaterType ?? "water grass";
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Apply orientation
            if (CmbMode.SelectedIndex == 2)
            {
                _variant.Orientation = null;
            }
            else
            {
                _variant.Orientation ??= new Orientation();
                _variant.Orientation.Mode = CmbMode.SelectedIndex == 0 ? "MinimalBoundingSquare" : "BoundingCircle";

                if (double.TryParse(TxtBaseAngleMin.Text, out var bMin))
                    _variant.Orientation.BaseAngleMin = bMin;
                if (double.TryParse(TxtBaseAngleMax.Text, out var bMax))
                    _variant.Orientation.BaseAngleMax = bMax;
                if (double.TryParse(TxtRandomAngleAmplitude.Text, out var rAmp))
                    _variant.Orientation.RandomAngleAmplitude = rAmp;
                if (double.TryParse(TxtRandomAngleStep.Text, out var rStep))
                    _variant.Orientation.RandomAngleStep = rStep;

                var zoneName = (CmbZeroAngleZone.SelectedItem as string) ?? CmbZeroAngleZone.Text.Trim();
                _variant.Orientation.ZeroAngleZone = string.IsNullOrEmpty(zoneName) ? null : zoneName;
            }

            // Apply border
            _variant.Border ??= new Border();

            if (double.TryParse(TxtCornerRadius.Text, out var cr))
                _variant.Border.CornerRadius = cr;
            if (int.TryParse(TxtObstaclesWidth.Text, out var ow))
                _variant.Border.ObstaclesWidth = ow;
            if (int.TryParse(TxtWaterWidth.Text, out var ww))
                _variant.Border.WaterWidth = ww;

            _variant.Border.WaterType = "water grass";

            if (double.TryParse(TxtObstaclesNoiseAmp.Text, out var oAmp) &&
                double.TryParse(TxtObstaclesNoiseFreq.Text, out var oFreq))
            {
                _variant.Border.ObstaclesNoise = [new NoiseEntry { Amp = oAmp, Freq = oFreq }];
            }

            if (double.TryParse(TxtWaterNoiseAmp.Text, out var wAmp) &&
                double.TryParse(TxtWaterNoiseFreq.Text, out var wFreq))
            {
                _variant.Border.WaterNoise = [new NoiseEntry { Amp = wAmp, Freq = wFreq }];
            }

            DialogResult = true;
            Close();
        }

        private void BtnClearBorder_Click(object sender, RoutedEventArgs e)
        {
            _variant.Border = null;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
