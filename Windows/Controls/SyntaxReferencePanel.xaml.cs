using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using MetroEditor.MetroLang;
using static MetroEditor.MetroLang.SyntaxUsage;

namespace MetroEditor.Windows.Controls
{
    public partial class SyntaxReferencePanel : UserControl
    {
        // 双击事件，传递选中的 SyntaxReferenceItem
        public event EventHandler<SyntaxReferenceItem>? ItemDoubleClicked;

        private ICollectionView _collectionView;

        public SyntaxReferencePanel()
        {
            InitializeComponent();
            _collectionView = CollectionViewSource.GetDefaultView(SyntaxUsage.SyntaxReferences);
            SyntaxList.ItemsSource = _collectionView;
        }

        private void SyntaxReferenceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is SyntaxReferenceItem item)
            {
                ItemDoubleClicked?.Invoke(this, item);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_collectionView == null) return;
            var text = SearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(text))
            {
                _collectionView.Filter = null;
                return;
            }
            _collectionView.Filter = obj =>
            {
                if (obj is SyntaxReferenceItem item)
                {
                    return (item.Command?.ToLower().Contains(text) == true) ||
                           (item.Format?.ToLower().Contains(text) == true) ||
                           (item.Example?.ToLower().Contains(text) == true);
                }
                return false;
            };
        }
    }
}
