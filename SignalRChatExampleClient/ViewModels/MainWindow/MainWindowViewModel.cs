using SignalRChatExampleClient.Commands;
using SignalRChatExampleClient.Conditions.MainWindowViewModel;
using SignalRChatExampleClient.Enums;
using SignalRChatExampleClient.Models;
using SignalRChatExampleClient.Modules.Chat.Interfaces;
using SignalRChatExampleClient.Modules.Dialog.Interfaces;
using SignalRChatExampleClient.Modules.MainWindow.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace SignalRChatExampleClient.ViewModels.MainWindow
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IChatService _chatService;

        private readonly IMWVMCommandExecuteConditionsService _mwvmCommandExecuteConditionsService;

        private readonly ILoginHandler _loginHandler;
        private readonly ILogoutHandler _logoutHandler;
        private readonly IDisconnectHandler _disconnectHandler;
        private readonly IMessageHandler _messageHandler;
        private readonly IReconnectHandler _reconnectHandler;

        private readonly INotificationDialogWindowService _notificationDialogWindowService;

        private readonly TaskFactory _ctxTaskFactory;

        public MainWindowViewModel(IChatService chatService, IMWVMCommandExecuteConditionsService mwvmCommandExecuteConditionsService,
                                   ILoginHandler loginHandler, ILogoutHandler logoutHandler, IDisconnectHandler disconnectHandler, IMessageHandler messageHandler,
                                   IReconnectHandler reconnectHandler, INotificationDialogWindowService notificationDialogWindowService, TaskFactory ctxTaskFactory)
        {
            if (ctxTaskFactory.Scheduler == null)
            {
                throw new ArgumentNullException(nameof(ctxTaskFactory.Scheduler));
            }

            _ctxTaskFactory = ctxTaskFactory;

            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _mwvmCommandExecuteConditionsService = mwvmCommandExecuteConditionsService ?? throw new ArgumentNullException(nameof(_mwvmCommandExecuteConditionsService));

            _loginHandler = loginHandler ?? throw new ArgumentNullException(nameof(loginHandler));
            _loginHandler.CtxTaskFactory = _ctxTaskFactory ?? throw new ArgumentNullException(nameof(_ctxTaskFactory));
            _loginHandler.Participants = Participants ?? throw new ArgumentNullException(nameof(Participants));

            _logoutHandler = logoutHandler ?? throw new ArgumentNullException(nameof(logoutHandler));
            _logoutHandler.Participants = Participants ?? throw new ArgumentNullException(nameof(logoutHandler));

            _disconnectHandler = disconnectHandler ?? throw new ArgumentNullException(nameof(disconnectHandler));
            _disconnectHandler.Participants = Participants ?? throw new ArgumentNullException(nameof(disconnectHandler));

            _reconnectHandler = reconnectHandler ?? throw new ArgumentNullException(nameof(reconnectHandler));
            _reconnectHandler.Participants = Participants ?? throw new ArgumentNullException(nameof(reconnectHandler));

            _messageHandler = messageHandler ?? throw new ArgumentException(nameof(messageHandler));
            _messageHandler.CtxTaskFactory = _ctxTaskFactory ?? throw new ArgumentNullException(nameof(_ctxTaskFactory));
            _messageHandler.Participants = Participants ?? throw new ArgumentNullException(nameof(Participants));

            _notificationDialogWindowService = notificationDialogWindowService ??
                                               throw new ArgumentNullException(nameof(notificationDialogWindowService));

            _chatService.NewMessage += NewMessage;
            _chatService.ParticipantLoggedIn += ParticipantLoggedIn;
            _chatService.ParticipantLoggedOut += ParticipantLoggedOut;
            _chatService.ParticipantDisconnected += ParticipantDisconnection;
            _chatService.ParticipantReconnected += ParticipantReconnection;
            _chatService.ConnectionReconnecting += Reconnecting;
            _chatService.ConnectionReconnected += Reconnected;
            _chatService.ConnectionClosed += Disconnected;

            _minDisplayTime = TimeSpan.FromSeconds(Convert.ToInt32(File.ReadAllLines("time_config.cfg")[0])); // tutaj czytam długość blokady z configu
        }

        #region PROPERTIES Getters/ Setters

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                _isLoggedIn = value;
                OnPropertyChanged();
            }
        }

        private string _userName;
        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged();
            }
        }

        private string _message;
        public string Message
        {
            get => _message;
            set
            {
                _message = value;

                OnPropertyChanged();
            }
        }

        private TimeSpan _minDisplayTime;
        public TimeSpan MinDisplayTime
        {
            get => _minDisplayTime;
            set
            {
                _minDisplayTime = value;

                OnPropertyChanged();
            }
        }

        private string _searchFilter;
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                _searchFilter = value;
                FilteredParticipants.Refresh();
            }
        }

        private ObservableCollection<ParticipantViewModel> _participants = new ObservableCollection<ParticipantViewModel>();
        public ObservableCollection<ParticipantViewModel> Participants
        {
            get => _participants;
            set
            {
                _participants = value;
                OnPropertyChanged();
            }
        }
        public ICollectionView FilteredParticipants
        {
            get
            {
                var source = CollectionViewSource.GetDefaultView(Participants);
                source.Filter = p => ParticipantsFilter(p);
                return source;
            }
        }

        private bool ParticipantsFilter(object p)
        {
            if (string.IsNullOrEmpty(SearchFilter))
                return true;

            ParticipantViewModel participant = (ParticipantViewModel)p;
            if (participant.Name.Contains(SearchFilter))
                return true;
            else
                return false;
        }

        private ParticipantViewModel _selectedParticipantViewModel;
        public ParticipantViewModel SelectedParticipantViewModel
        {
            get => _selectedParticipantViewModel;
            set
            {
                _selectedParticipantViewModel = value;

                if (SelectedParticipantViewModel != null)
                {
                    if (SelectedParticipantViewModel.HasSentNewMessage)
                    {
                        SelectedParticipantViewModel.HasSentNewMessage = false;
                    }

                    OnPropertyChanged();
                }
            }
        }

        private ChatModeType _chatChatModeType;
        public ChatModeType ChatModeType
        {
            get => _chatChatModeType;
            set
            {
                _chatChatModeType = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region COMMANDS

        private ICommand _connectCommand;
        public ICommand ConnectCommand =>
            _connectCommand ?? (_connectCommand = new RelayCommandAsync(Connect));


        private ICommand _loginCommand;
        public ICommand LoginCommand =>
            _loginCommand ?? (_loginCommand =
                new RelayCommandAsync(Login, o => _mwvmCommandExecuteConditionsService.CanLogin(UserName, IsConnected)));


        private ICommand _logoutCommand;
        public ICommand LogoutCommand =>
            _logoutCommand ?? (_logoutCommand =
                new RelayCommandAsync(Logout, o => _mwvmCommandExecuteConditionsService.CanLogout(IsConnected, IsLoggedIn)));


        private ICommand _sendMessageCommand;
        public ICommand SendMessageCommand =>
            _sendMessageCommand ?? (_sendMessageCommand =
                new RelayCommandAsync(SendUnicastMessage, o =>
                    _mwvmCommandExecuteConditionsService.CanSendMessage(Message, IsConnected, _selectedParticipantViewModel)));


        private ICommand _sendUnicastNotificationCommand;

        public ICommand SendUnicastNotificationCommand =>
            _sendUnicastNotificationCommand ?? (_sendUnicastNotificationCommand =
                new RelayCommandAsync(SendUnicastNotification, o =>
                    _mwvmCommandExecuteConditionsService.CanSendMessage(Message, IsConnected, _selectedParticipantViewModel)));

        private ICommand _sendSpecialUnicastNotificationCommand;

        public ICommand SendSpecialUnicastNotificationCommand =>
            _sendSpecialUnicastNotificationCommand ?? (_sendSpecialUnicastNotificationCommand =
                new RelayCommandAsync(SendSpecialUnicastNotification, o =>
                    _mwvmCommandExecuteConditionsService.CanSendMessage(Message, IsConnected, _selectedParticipantViewModel)));

        private ICommand _sendBroadcastMessageCommand;
        public ICommand SendBroadcastMessageCommand =>
            _sendBroadcastMessageCommand ?? (_sendBroadcastMessageCommand = new RelayCommandAsync(
                SendBroadcastMessage, o =>
                    _mwvmCommandExecuteConditionsService.CanSendBroadcastMessage(Message, IsConnected)));

        private ICommand _sendSpecialBroadcastMessageCommand;
        public ICommand SendSpecialBroadcastMessageCommand =>
            _sendSpecialBroadcastMessageCommand ?? (_sendSpecialBroadcastMessageCommand = new RelayCommandAsync(
                SendSpecialBroadcastMessage, o =>
                 _mwvmCommandExecuteConditionsService.CanSendBroadcastMessage(Message, IsConnected)));

        #endregion

        #region PRIVATE COMMAND Helper Methods

        private async Task<bool> Connect()
        {
            try
            {
                await _chatService.ConnectAsync();

                IsConnected = true;

                return true;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("Could not open connection to server!");

                Debug.WriteLine($"++++ ERROR - Could not open connection to server! See more in Stack Trace: {ex.StackTrace}\n\n" +
                                $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
        }

        private async Task<bool> Login()
        {
            try
            {
                bool loggedIsSuccess = await _chatService.LoginAsync(_userName);

                if (loggedIsSuccess)
                {
                    List<User> loggedUsers = await _chatService.GetLoggedUsersAsync();

                    if (loggedUsers.Any())
                    {
                        ChatModeType = ChatModeType.Chat;
                        IsLoggedIn = true;

                        foreach (User loggedUser in loggedUsers.Where(item => item.UserName != _userName))
                        {
                            Participants.Add(new ParticipantViewModel
                            {
                                ConnectionId = loggedUser.ConnectionId,
                                Name = loggedUser.UserName,
                                IsLoggedIn = true
                            });
                        }

                        return true;
                    }

                    _notificationDialogWindowService.ShowErrorMessageBox("Internal server error! User not found!");

                    return false;
                }

                _notificationDialogWindowService.ShowWarningMessageBox("Username is exists! Plase choose another one!");

                return false;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("There was an error signing in!");

                Debug.WriteLine($"++++ ERROR - There was an error signing in! See more in Stack Trace: {ex.StackTrace}\n\n" +
                                $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
        }

        private async Task<bool> Logout()
        {
            try
            {
                await _chatService.LogoutAsync();

                return true;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("There was an error logout!");

                Debug.WriteLine($"++++ ERROR - There was an error logout! See more in Stack Trace: {ex.StackTrace}\n\n" +
                                $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
        }

        private async Task<bool> SendUnicastMessage()
        {
            try
            {
                await _chatService.SendUnicastMessageAsync(_selectedParticipantViewModel.ConnectionId, _message, DateTime.Now);

                SelectedParticipantViewModel.Messages.Add(new ChatMessage
                {
                    SenderName = UserName,
                    Message = _message,
                    MessagePostedDateTime = DateTime.Now,
                    IsSenderMessage = true
                });

                return true;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("There was an error sending the unicast message!");

                Debug.WriteLine(
                    $"++++ ERROR - There was an error sending the unicast message! See more in Stack Trace: {ex.StackTrace}\n\n" +
                    $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
            finally
            {
                Message = string.Empty;
            }
        }

        private async Task<bool> SendUnicastNotification()
        {
            return await SendUnicastNotification(TimeSpan.Zero);
        }

        private async Task<bool> SendSpecialUnicastNotification()
        {
            return await SendUnicastNotification(MinDisplayTime);
        }

        private async Task<bool> SendUnicastNotification(TimeSpan minDisplayTime)
        {
            try
            {
                await _chatService.SendUnicastNotificationAsync(_selectedParticipantViewModel.ConnectionId, _message, DateTime.Now, minDisplayTime);

                return true;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("There was an error sending the unicast notification!");

                Debug.WriteLine($"++++ ERROR - There was an error sending the unicast notification! See more in Stack Trace: {ex.StackTrace}\n\n" +
                                $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
            finally
            {
                Message = string.Empty;
            }
        }

        private async Task<bool> SendBroadcastMessage()
        {
            return await SendBroadcastMessage(TimeSpan.Zero);
        }

        private async Task<bool> SendSpecialBroadcastMessage()
        {
            return await SendBroadcastMessage(MinDisplayTime);
        }

        private async Task<bool> SendBroadcastMessage(TimeSpan minDisplayTime)
        {
            try
            {
                await _chatService.SendBroadcastMessageAsync(_message, DateTime.Now, minDisplayTime);

                return true;
            }
            catch (Exception ex)
            {
                _notificationDialogWindowService.ShowErrorMessageBox("There was an error sending the broadcast message!");

                Debug.WriteLine($"++++ ERROR - There was an error sending the broadcast message! See more in Stack Trace: {ex.StackTrace}\n\n" +
                                $"See more in Inner Exception: {ex.InnerException}\n\nSee more in Message: {ex.Message}");

                return false;
            }
            finally
            {
                Message = string.Empty;
            }
        }

        #endregion

        #region "EVENT HANDLERS". The events that are responsible for updating the UI

        private void ParticipantLoggedIn(User loggedUser)
        {
            if (_isLoggedIn && _loginHandler.ParticipantIsExists(loggedUser))
            {
                _loginHandler.AddNewParticipant(loggedUser);
            }
        }

        private void ParticipantLoggedOut(string loggedOutUserName)
        {
            ParticipantViewModel loggedOutParticipantViewModel = _logoutHandler.GetLoggedOutParticipant(loggedOutUserName);

            if (loggedOutParticipantViewModel != null)
            {
                _logoutHandler.ClearLoggedOutParticipantChatMessages(loggedOutParticipantViewModel);

                _ctxTaskFactory.StartNew(() => loggedOutParticipantViewModel.Messages).Wait();

                _logoutHandler.RemoveLoggedOutParticipantFromParticipantList(loggedOutParticipantViewModel);
            }
            else
            {
                Debug.WriteLine($"++++ ERROR - Participant not found in Participant List! Caller Method {nameof(ParticipantLoggedOut)}");
            }
        }

        private void ParticipantDisconnection(string disconnectionConnectionId)
        {
            ParticipantViewModel disconnectedParticipantViewModel =
                _disconnectHandler.GetDisconnectedParticipant(disconnectionConnectionId);

            if (disconnectedParticipantViewModel != null)
            {
                _disconnectHandler.ClearDisconnectedParticipantChatMessages(disconnectedParticipantViewModel);

                _ctxTaskFactory.StartNew(() => disconnectedParticipantViewModel.Messages).Wait();

                _disconnectHandler.RemoveDisconnectedParticipantFromParticipantList(disconnectedParticipantViewModel);
            }
            else
            {
                Debug.WriteLine($"++++ ERROR - Participant not found in Participant List! Caller Method {nameof(ParticipantDisconnection)}");
            }
        }

        private void ParticipantReconnection(string reconnectionConnectionId)
        {
            ParticipantViewModel reconnectionParticipantViewModel =
                _reconnectHandler.GetReconnectionParticipant(reconnectionConnectionId);

            if (reconnectionParticipantViewModel != null)
            {
                reconnectionParticipantViewModel.IsLoggedIn = true;
            }
        }

        private void NewMessage(string senderConnectionId, string message, DateTime messagePostedDateTime, TimeSpan minDisplayTime, MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Unicast:
                    _messageHandler.SendUnicastMessage(senderConnectionId, message, messagePostedDateTime, SelectedParticipantViewModel);
                    break;
                case MessageType.Broadcast:
                    _messageHandler.SendBroadcastMessage(senderConnectionId, message, messagePostedDateTime, minDisplayTime);
                    break;
                case MessageType.UnicastNotification:
                    _messageHandler.SendUnicastNotification(senderConnectionId, message, messagePostedDateTime, minDisplayTime);
                    break;
            }
        }

        private void Reconnecting()
        {
            IsConnected = false;
            IsLoggedIn = false;
        }

        private async void Reconnected()
        {
            if (string.IsNullOrEmpty(_userName))
                return;

            await _chatService.LoginAsync(_userName);

            IsConnected = true;
            IsLoggedIn = true;
        }

        private async void Disconnected()
        {
            await _chatService.ConnectAsync().ContinueWith(task =>
            {
                if (!task.IsFaulted)
                {
                    IsConnected = true;

                    _chatService.LoginAsync(_userName).Wait();

                    IsLoggedIn = true;
                }
            });
        }

        #endregion
    }
}
