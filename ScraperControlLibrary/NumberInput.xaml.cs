using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ScraperControlLibrary
{
    /// <summary>
    /// Interaction logic for NumberInput.xaml
    /// </summary>
    public partial class NumberInput : UserControl
    {
        public double InputValue
        {
            get { return (double)GetValue(InputValueProperty); }
            set { SetValue(InputValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InputValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InputValueProperty =
            DependencyProperty.Register("InputValue", typeof(double), typeof(NumberInput), new PropertyMetadata(default));





        public double Increments
        {
            get { return (double)GetValue(IncrementsProperty); }
            set { SetValue(IncrementsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Increments.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IncrementsProperty =
            DependencyProperty.Register("Increments", typeof(double), typeof(NumberInput), new PropertyMetadata(1.0));



        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Maximum.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(NumberInput), new PropertyMetadata(default));



        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Minimum.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(NumberInput), new PropertyMetadata(default));




        public RelayCommand Increment => new RelayCommand(x =>
        {
            var o = this;
            var val = x is bool b && b ? InputValue + Increments : InputValue - Increments;
            if (val > Maximum && Maximum > 0) val = Maximum;
            if (val < Minimum) val = Minimum;
            InputValue = val;
        });

        public NumberInput()
        {
            InitializeComponent();
        }
        public static RelayValueConverter ToDouble = new RelayValueConverter(x => x, x =>
        {
            var converted = Convert.ChangeType(x, TypeCode.Double);
            return converted;
        });
    }
    public class BoxChecker : ValidationRule
    {

        public BoxChecker()
        {
            ValidatesOnTargetUpdated = true;
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var foo = (value is string s && !double.TryParse(s, out var num)) ? new ValidationResult(false, "Not A Number") : ValidationResult.ValidResult;
            return foo;
        }
    }
}
