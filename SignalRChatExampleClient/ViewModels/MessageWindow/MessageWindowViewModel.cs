using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace SignalRChatExampleClient.ViewModels.MessageWindow
{
    public class MessageWindowViewModel : ViewModelBase
    {
        #region PROPERTIES Getters/ Setters

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;

                OnPropertyChanged();
            }
        }

        private string _contentTitle;
        public string ContentTitle
        {
            get => _contentTitle;
            set
            {
                _contentTitle = value;

                OnPropertyChanged();
            }
        }

        private string _senderName;
        public string SenderName
        {
            get => _senderName;
            set
            {
                _senderName = value;

                OnPropertyChanged();
            }
        }

        private DateTime _messagePostedTime;
        public DateTime MessagePostedTime
        {
            get => _messagePostedTime;
            set
            {
                _messagePostedTime = value;

                OnPropertyChanged();
            }
        }

        private string _closeBtnText = "Close";
        public string CloseBtnText
        {
            get => _closeBtnText;
            set 
            { 
                _closeBtnText = value; 
                
                OnPropertyChanged();
            }
        }

        private TimeSpan _minDisplayTime;
        private int secsElapsed = 0;
        public TimeSpan MinDisplayTime
        {
            get => _minDisplayTime;
            set
            {
                _minDisplayTime = value;
                OnPropertyChanged();

                if (_minDisplayTime != TimeSpan.Zero)
                {
                    CloseBtnText = "Close (" + _minDisplayTime.TotalSeconds.ToString() + ')';
                    DispatcherTimer timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(1); ;
                    timer.Tick += (obj, sender) =>
                    {
                        if (++secsElapsed >= _minDisplayTime.TotalSeconds)
                        {
                            ClosingAllowed = true;
                            CloseBtnText = "Close";
                            timer.Stop();
                        }
                        else
                            CloseBtnText = "Close (" + (_minDisplayTime.TotalSeconds - secsElapsed).ToString() + ')';
                    };
                    timer.Start();
                }
                else
                    ClosingAllowed = true;
            }
        }

        private bool _closingAllowed = false;
        public bool ClosingAllowed
        {
            get => _closingAllowed;
            set
            {
                _closingAllowed = value;

                OnPropertyChanged();
            }
        }

        private string _content;
        public string Content
        {
            get => _content;
            set
            {
                _content = value;

                OnPropertyChanged();
            }
        }

        #endregion

        #region COMMANDS

        private ICommand _closeBtnCommand;
        public ICommand CloseBtnCommand =>
            _closeBtnCommand;

        #endregion

    }
}
