using Pixelery.Views;
using WinRT;

// DaisySelect forwards its managed ItemsSource property to a native ListView.
// That indirect boundary is not visible to the automatic CsWinRT analysis.
[assembly: GeneratedWinRTExposedExternalType(typeof(List<SelectOption>))]
// Flowery also builds the added/removed item lists for SelectionChangedEventArgs.
[assembly: GeneratedWinRTExposedExternalType(typeof(List<object>))]
