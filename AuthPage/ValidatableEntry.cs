using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnrealFrontiersLauncher
{
    public class ValidatableEntry : INotifyPropertyChanged
    {
        private string _value="";
        private string _validationMessage;
        private bool _isValid;
        private bool _wasValidated;
        private Func<string, (bool, string)> _validationFunction;
        private AuthPageViewModel _authPage;

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                    Validate();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged(nameof(ValidationMessage));
                }
            }
        }

        public bool IsVisible => !IsValid && _wasValidated  && _authPage.IsRegisterMode;
        public bool IsValid
        {
            get => _isValid;
            private set
            {
                if (_isValid != value)
                {
                    _isValid = value;
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }


        public ValidatableEntry(Func<string, (bool, string)> validationFunction, AuthPageViewModel authPage)
        {
            _authPage = authPage;
            _validationFunction = validationFunction;
        }

        public void Validate()
        {
            _wasValidated = true;
            OnPropertyChanged(nameof(IsVisible));
            var (isValid, message) = _validationFunction(Value);
            IsValid = isValid;
            ValidationMessage = message;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
