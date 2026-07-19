using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static MetroEditor.MetroLang.SyntaxUsage;

namespace MetroEditor.Windows.Controls
{
    public partial class SyntaxReferencePanel : UserControl
    {
        // 双击事件，传递选中的 SyntaxReferenceItem
        public event EventHandler<SyntaxReferenceItem>? ItemDoubleClicked;

        public SyntaxReferencePanel()
        {
            InitializeComponent();
        }

        private void SyntaxReferenceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is SyntaxReferenceItem item)
            {
                ItemDoubleClicked?.Invoke(this, item);
            }
        }
    }
}
