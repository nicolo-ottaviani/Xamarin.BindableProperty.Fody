using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace TestApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        [Bindable]
        public string EnteredText { get; set; }

        public void OnEnteredTextChanged(string value)
        {
            LabelColor = (LabelColor == Color.DarkRed) ? Color.Gray : Color.DarkRed;
            SomeDouble = 0.1d * value.Length;
        }

        [Bindable]
        public Color LabelColor { get; set; }

        [Bindable]
        public double SomeDouble { get; set; }

        //public static BindableProperty LabelColorProperty = BindableProperty.Create(nameof(LabelColor), typeof(Color), typeof(MainPage), new Color(), BindingMode.TwoWay, null, null, null);
        //public Color LabelColor { get { return (Color)GetValue(LabelColorProperty); } set { SetValue(LabelColorProperty, value); } }

        //static void PIPPO()
        //{
        //    Label2ColorProperty = BindableProperty.Create(nameof(Label2Color), typeof(Color), typeof(MainPage), new Color(), BindingMode.TwoWay, null, null, null);
        //}

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class BindableAttribute : Attribute { }
}
