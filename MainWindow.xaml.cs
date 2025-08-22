using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using calculator;

namespace calculator
{
    public class CalculatorController
    {
        private double acc = 0.0;
        private string display = "0";
        private string? equationTop = null;
        private bool errorState = false;
        private bool isEntering = false;
        private string? lastOp = null;
        private double lastOperand = 0.0;
        private double memory = 0.0;
        private string? pendingOp = null;

        // stores "a op b =" after '='
        public bool HasMemory => Math.Abs(memory) > 0;

        public string GetDisplay() => errorState ? "Chyba" : display;

        public string GetTopLine()
        {
            if (errorState) return "";
            if (!string.IsNullOrEmpty(equationTop)) return equationTop;
            return pendingOp != null ? $"{OperationManager.Format(acc)} {pendingOp}" : "";
        }

        public void ProcessToken(string token)
        {
            if (errorState && token is not "C" and not "CE") return;

            if (OperationManager.IsDigit(token))
            {
                StartIfNeeded();
                display = OperationManager.AppendDigit(display, token);
                display = OperationManager.FormatUserTyping(display); // live grouping
                return;
            }

            switch (token)
            {
                case ".":
                    StartIfNeeded();
                    display = OperationManager.AppendDecimal(display);
                    display = OperationManager.FormatUserTyping(display); // keep grouping while typing
                    break;

                case "+":
                case "-":
                case "*":
                case "/":
                case "^":
                    OnOperator(token);
                    break;

                case "=":
                    OnEquals();
                    break;

                case "CE":
                    display = "0"; isEntering = true; if (errorState) errorState = false;
                    break;

                case "C":
                    ResetAll();
                    break;

                case "BS":
                    if (isEntering) display = OperationManager.Backspace(display);
                    display = OperationManager.FormatUserTyping(display); // re-group after backspace
                    break;

                case "+/-":
                    display = OperationManager.NegateString(display);
                    display = OperationManager.FormatUserTyping(display); // keep grouping with sign change
                    break;

                case "%":
                    OnPercentKey();
                    break;

                case "1/x":
                    if (TryGetDisplay(out var x)) display = OperationManager.Reciprocal(x, SetError);
                    isEntering = true; equationTop = null;
                    break;

                case "x2":
                    if (TryGetDisplay(out x)) display = OperationManager.Square(x);
                    isEntering = true; equationTop = null;
                    break;

                case "sqrt":
                    if (TryGetDisplay(out x)) display = OperationManager.Sqrt(x, SetError);
                    isEntering = true; equationTop = null;
                    break;

                case "MC": memory = 0; break;
                case "MR": display = OperationManager.Format(memory); isEntering = true; break;
                case "M+": if (TryGetDisplay(out x)) { memory += x; } break;
                case "M-": if (TryGetDisplay(out x)) { memory -= x; } break;

                default: break;
            }
        }

        private bool ApplyPending()
        {
            if (!TryGetDisplay(out var right)) return false;
            var ok = SafeCompute(pendingOp!, right);
            if (ok)
            {
                display = OperationManager.Format(acc);
                isEntering = false;
            }
            return ok;
        }

        private void OnEquals()
        {
            if (pendingOp != null)
            {
                double right;

                if (isEntering)
                {
                    if (!TryGetDisplay(out right)) return;
                    lastOperand = right;          // remember for repeated "="
                }
                else
                {
                    // User pressed "=" without typing the right operand:
                    lastOperand = acc;
                }

                lastOp = pendingOp;               // remember operator for repeated "="
                var leftForEq = acc;              // for the equation line

                acc = OperationManager.Compute(acc, pendingOp, lastOperand, SetError);
                display = OperationManager.Format(acc);
                equationTop = $"{OperationManager.Format(leftForEq)} {lastOp} {OperationManager.Format(lastOperand)} =";

                pendingOp = null;
                isEntering = false;
                return;
            }

            // no pending op, but we have lastOp/lastOperand -> repeat operation
            if (lastOp != null)
            {
                var leftForEq = acc;

                acc = OperationManager.Compute(acc, lastOp, lastOperand, SetError);
                display = OperationManager.Format(acc);
                equationTop = $"{OperationManager.Format(leftForEq)} {lastOp} {OperationManager.Format(lastOperand)} =";

                isEntering = false;
            }
        }

        private void OnOperator(string op)
        {
            if (pendingOp != null && isEntering)
            {
                if (!ApplyPending()) return;
            }
            else if (pendingOp == null && TryGetDisplay(out var val))
            {
                acc = val;
            }
            pendingOp = op;
            isEntering = false;
            equationTop = null; // clear equation line when new op starts
        }

        private void OnPercentKey()
        {
            if (pendingOp == null) return;
            if (!TryGetDisplay(out var b)) return;

            var transformed = OperationManager.PercentTransform(acc, pendingOp, b);
            display = OperationManager.Format(transformed);
            isEntering = true;
            equationTop = null;
        }

        private void ResetAll()
        {
            display = "0"; acc = 0; pendingOp = null; isEntering = false; errorState = false;
            lastOp = null; lastOperand = 0; equationTop = null;
        }

        private bool SafeCompute(string op, double right)
        {
            try
            {
                acc = OperationManager.Compute(acc, op, right, SetError);
                return !errorState;
            }
            catch { SetError(); return false; }
        }

        private void SetError()
        { display = "Chyba"; errorState = true; }

        private void StartIfNeeded()
        {
            if (!isEntering)
            {
                display = "0";
                isEntering = true;
                equationTop = null; // typing a new number clears equation line
            }
        }

        private bool TryGetDisplay(out double val) =>
                            OperationManager.TryParse(display, out val);
    }

    //Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        private readonly CalculatorController _controller = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadTheme();
            Render();
        }

        // Helper to swap theme dictionary at runtime
        private void ApplyTheme(string theme) // "Light" or "Dark"
        {
            //read current color from Window
            var oldBrush = (SolidColorBrush)(this.Background ?? Brushes.Transparent);
            var oldColor = oldBrush.Color;

            //swap dictionaries (as you вже робиш)
            var dicts = Application.Current.Resources.MergedDictionaries;
            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                var src = dicts[i].Source?.ToString() ?? "";
                if (src.Contains("/Light.xaml") || src.Contains("/Dark.xaml"))
                    dicts.RemoveAt(i);
            }
            dicts.Add(new ResourceDictionary { Source = new Uri($"{theme}.xaml", UriKind.Relative) });

            //get target color from theme resource "Brush.Background"
            var targetBrush = (SolidColorBrush)FindResource("Brush.Background");
            var targetColor = targetBrush.Color;

            //set a fresh brush so it's animatable (do NOT reuse a frozen brush)
            var animBrush = new SolidColorBrush(oldColor);
            this.Background = animBrush;

            //animate color
            var anim = new ColorAnimation
            {
                To = targetColor,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            animBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        private void LoadTheme()
        {
            // Always start with Light theme
            ApplyTheme("Light");
            ThemeToggle.IsChecked = false; // unchecked = Light, checked = Dark
        }

        // Event handler for all buttons
        private void OnButton(object sender, RoutedEventArgs e)
        {
            var token = (string)((Button)sender).Tag;
            _controller.ProcessToken(token);
            Render();
        }

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            string? token = null;
            //NumPad
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                token = ((int?)(e.Key - Key.NumPad0)).ToString();
            }
            else
            {
                switch (e.Key)
                {
                    // NumPad operators
                    case Key.Add: token = "+"; break;
                    case Key.Subtract: token = "-"; break;
                    case Key.Multiply: token = "*"; break;
                    case Key.Divide: token = "/"; break;
                    case Key.Decimal: token = "."; break;

                    // Confirm / edit / clear
                    case Key.Enter: token = "="; break;
                    case Key.Back: token = "BS"; break;
                    case Key.Delete: token = "CE"; break;  // Windows: Delete == CE
                    case Key.Escape: token = "C"; break;
                    case Key.F9: token = "+/-"; break; // Windows: F9 toggles sign

                    // Top-row operators (optional)
                    case Key.OemPlus:
                        token = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? "+" : "=";
                        break;

                    case Key.OemMinus:
                        token = "-"; break;
                    case Key.Oem2:
                        token = "/"; break;
                    case Key.OemPeriod:
                    case Key.OemComma:
                        token = "."; break;
                }
            }

            if (token != null)
            {
                _controller.ProcessToken(token);
                Render();
                e.Handled = true;
            }
        }

        // Printable characters (digits, + - * / . , % ^ =)
        private void OnTextInput(object? sener, TextCompositionEventArgs e)
        {
            string t = e.Text;
            string? token = t switch
            {
                "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" => t,
                "." or "," => ".",
                "+" or "-" or "*" or "/" => t,
                "%" => "%",
                "=" => "=",
                "^" => "^",
                _ => null
            };
            if (token is null) return;

            _controller.ProcessToken(token);
            Render();
            e.Handled = true;
        }

        // Sync UI with controller state (auto-scroll + adaptive font)
        private void Render()
        {
            // main display text (already formatted by controller/manager)
            DisplayText.Text = _controller.GetDisplay();

            // top expression line (shows "a op" during input, or "a op b =" after '=')
            Expression.Text = _controller.GetTopLine();

            // memory flag ("M" when memory != 0)
            MemoryFlag.Text = _controller.HasMemory ? "M" : "";
        }

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Dark ON
            ApplyTheme("Dark");
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Light ON
            ApplyTheme("Light");
        }
    }
}