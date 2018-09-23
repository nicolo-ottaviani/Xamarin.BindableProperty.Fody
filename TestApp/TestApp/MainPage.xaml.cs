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
        }

        [Bindable]
        public string EnteredText { get; set; }

        public void OnEnteredTextChanged(string value)
        {
            label.BackgroundColor = (label.BackgroundColor == Color.DarkRed) ? Color.Gray : Color.DarkRed;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class BindableAttribute : Attribute { }
}
