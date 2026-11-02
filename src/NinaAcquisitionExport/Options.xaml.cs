using System.ComponentModel.Composition;
using System.Windows;

namespace NinaAcquisitionExport;

[Export(typeof(ResourceDictionary))]
public partial class Options : ResourceDictionary {
    public Options() {
        InitializeComponent();
    }
}
