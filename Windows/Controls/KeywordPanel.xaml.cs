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

namespace MetroEditor.Windows.Controls
{
    public partial class KeywordPanel : UserControl
    {
        // 关键字点击事件，传递关键字字符串
        public event EventHandler<string>? KeywordClicked;

        public KeywordPanel()
        {
            InitializeComponent();
        }

        private void KeywordButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string keyword })
            {
                KeywordClicked?.Invoke(this, keyword);
            }
        }
    }
}
