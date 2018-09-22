# Xamarin.BindableProperty.Fody

An assembly weaver, based on Fody, that automatically transforms plain auto-implemented properties into BindableProperties that can be used in Xamarin Forms.

## Installation

Compile the BindableProperty project in order to build the NuGet package (it is in the "nuget" directory in the solution folder). Add this nuget package to your prject. Then, add a file called FodyWeavers.xml in your project root (build action must be set to Content), and write the following inside:
    
    <?xml version="1.0" encoding="utf-8"?>
    <Weavers>
        <BindableProperty />
    </Weavers>
 
 See the Fody documentation for more info on the FodyWeavers.xml file.

## Usage

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
