#Xamarin.BindableProperty.Fody

An assembly weaver, based on Fody, that automatically transforms plain auto-implemented properties into BindableProperties that can be used in Xamarin Forms.

##Usage

Just decorate an auto-implemented get/set property with the Bindable attribute.

For example, this:

    public class MyButton : Xamarin.Forms.Button
    {
        [Bindable]
        public string FontFamily { get; set; }
    }

will become like this:

    public class MyButton : Xamarin.Forms.Button
    {
        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(Button), null, null);
        public string FontFamily { get { return (string)GetValue(FontFamilyProperty); } set { SetValue(FontFamilyProperty, value); } }
    }

The public static readonly XxxProperty is automatically generated if not present. 
The getter and setter methods are automatically implemented with GetValue(...) and SetValue(...) methods, respectively.
The Bindable attribute is removed from the resulting dll.
