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

        public static BindableProperty SomeColorNullableProperty = BindableProperty.Create(nameof(SomeColorNullable), typeof(Color?), typeof(MainPage), default(Color?), BindingMode.TwoWay, null, null, null);
        public Color? SomeColorNullable { get { return (Color?)GetValue(SomeColorNullableProperty); } set { SetValue(SomeColorNullableProperty, value); } }

        [Bindable]
        public string EnteredText { get; set; }

        public void OnEnteredTextChanged(string value)
        {
            LabelColor = (LabelColor == null) ? Color.Gray : (Color?)null;
            SomeDouble = 0.1d * (value?.Length ?? 0);
            SomeList = SomeList ?? new List<int>();
            SomeList.Add(SomeList.Count);
        }

        [Bindable]
        public Color? LabelColor { get; set; }

        [Bindable]
        public double SomeDouble { get; set; }

        [Bindable]
        public List<int> SomeList { get; set; }

        public int SomeListMax => SomeList?.Max() ?? -1;

        public Color LabelColorNotNull => LabelColor ?? Color.DarkRed;

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class BindableAttribute : Attribute { }
}
