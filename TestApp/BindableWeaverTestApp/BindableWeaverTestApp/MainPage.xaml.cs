using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace BindableWeaverTestApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            this.BindingContext = this;
        }


        [Bindable]
        public string EnteredText { get; set; }

        private void OnEnteredTextChanged(string value)
        {
            Console.WriteLine("EnteredText has changed.");
            label.BackgroundColor = (label.BackgroundColor == Color.Red) ? Color.Green : Color.Red;
        }

        //[Bindable]
        //public int SomeInteger { get; set; }

        [Bindable]
        public double SomeDouble { get; set; }




        public static BindableProperty SomeIntegerProperty = BindableProperty.Create("SomeText", typeof(int), typeof(MainPage), 0, BindingMode.TwoWay, null, __On_SomeInteger_Changed, null, null, null);
        public int SomeInteger { get { return (int)GetValue(SomeIntegerProperty); } set { SetValue(SomeIntegerProperty, value); } }

        private static void __On_SomeInteger_Changed(BindableObject x, object o, object n)
        {
            ((MainPage)x).On_SomeInteger_Changed((int)n);
        }
        private void On_SomeInteger_Changed(int value)
        {
            Console.WriteLine($"SomeText has changed to {value}.");
        }

	}

    [AttributeUsage(AttributeTargets.Property)]
    public class BindableAttribute : Attribute
    {
    }
}
