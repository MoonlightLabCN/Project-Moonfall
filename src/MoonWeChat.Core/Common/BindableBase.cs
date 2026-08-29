using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MoonWeChat.Common
{
    /// <summary>
    /// 简单的 INotifyPropertyChanged 基类，供各 ViewModel 继承。
    /// 没有引入 MVVM Toolkit / MVVM Light 之类的第三方包，保持项目零额外依赖，
    /// 方便直接拖进已有工程编译。
    /// </summary>
    public abstract class BindableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
