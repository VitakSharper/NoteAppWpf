using System.Windows;
using System.Windows.Controls;
using NoteApp.Domain.Models;
using NoteApp.ViewModels;

namespace NoteApp.Views;

public sealed class BlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextBlockTemplate { get; set; }
    public DataTemplate? FileBlockTemplate { get; set; }
    public DataTemplate? LinkBlockTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is BlockViewModel vm
            ? vm.BlockType switch
            {
                BlockType.Text => TextBlockTemplate,
                BlockType.File => FileBlockTemplate,
                BlockType.Link => LinkBlockTemplate,
                _ => base.SelectTemplate(item, container)
            }
            : base.SelectTemplate(item, container);
}
