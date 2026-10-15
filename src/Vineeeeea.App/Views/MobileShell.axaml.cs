using System;
using Avalonia.Controls;
using Avalonia;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.App.Views;

public partial class MobileShell : UserControl
{
    private readonly IToolRunner? toolRunner;

    public MobileShell()
        : this(null)
    {
    }

    public MobileShell(IToolRunner? toolRunner)
    {
        this.toolRunner = toolRunner;
        InitializeComponent();
    }

    /// <summary>The storage provider belongs to the top level, which only exists once the view is on screen.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is null && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            DataContext = CompositionRoot.CreateMainViewModel(
                topLevel, AppConfig.FromEnvironment(Environment.GetEnvironmentVariable), new EditorSession(), toolRunner);
        }
    }
}
