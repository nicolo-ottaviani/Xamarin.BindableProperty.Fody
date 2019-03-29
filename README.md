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

#OnXxxChanged method

If you want some piece of code to be called whenever the property value changes, simply add a void OnXxxChanged method, where Xxx is the name of the property. It will be registered on the ValueChanged delegate in the BindableProperty.Create method:

    public class MyButton : Xamarin.Forms.Button
    {
        [Bindable]
        public string FontFamily { get; set; }
        
        private void OnFontFamilyChanged(string newValue) { 
            //do stuff here
            Console.WriteLine("Property value is changed");
        }
    }

The OnXxxChanged method must have only one parameter  (whose type must match the type of the property) that will contain the new value.
The OnXxxChanged method can be public or private as well.

# Dependent properties

If you have some read-only properties that depend on a bindable property, the OnPropertyChanged event is called automatically when the value of the bindable property changes.
For example, the following code:

    [Bindable]
    public string FontFamily { get; set; }    
    public string FontFamilyUpper  => FontFamily?.ToUpper();
    public string FontFamilyLength => FontFamily?.Length ?? 0;

Would become like this:

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(Button), null, null);
    public string FontFamily { 
        get { return (string)GetValue(FontFamilyProperty); } 
        set { 
            SetValue(FontFamilyProperty, value); 
            this.OnPropertyChanged("FontFamilyUpper");
            this.OnPropertyChanged("FontFamilyLength");
        }
    }

For now, you should <b>not use Fody.PropertyChanged</b> togheter with this package, because the code weaving alghoritms make conflicting modifications and will make your code behave unexpectedly.

# Other info

The actual weaver code is in the BindablePropery.Fody project, while the BindableProperty project serves to build the NuGet package.

