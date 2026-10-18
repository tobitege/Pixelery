namespace Pixelery.Views;

/// <summary>Rejects invalid edits before they enter a custom dimension field, including pasted text.</summary>
internal sealed partial class DimensionInput(Func<string, bool> acceptsText) : DaisyInput
{
    protected override void OnLoaded()
    {
        base.OnLoaded();
        if (InnerTextBox is { } textBox)
            textBox.BeforeTextChanging += BeforeTextChanging;
    }

    protected override void OnUnloaded()
    {
        if (InnerTextBox is { } textBox)
            textBox.BeforeTextChanging -= BeforeTextChanging;
        base.OnUnloaded();
    }

    private void BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args) =>
        args.Cancel = !acceptsText(args.NewText);
}
