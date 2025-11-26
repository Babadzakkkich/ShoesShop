using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShoesShop.Pages
{
    public partial class AuthPage : Page
    {
        private MainWindow _mainWindow;

        public AuthPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoginTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptLogin();
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.LoginAsGuest();
        }

        private void AttemptLogin()
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            // Валидация
            if (string.IsNullOrEmpty(login))
            {
                _mainWindow.ShowError("Введите логин", "Ошибка ввода");
                LoginTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                _mainWindow.ShowError("Введите пароль", "Ошибка ввода");
                PasswordBox.Focus();
                return;
            }

            try
            {
                // Показываем индикатор загрузки (можно добавить позже)
                LoginButton.IsEnabled = false;
                GuestButton.IsEnabled = false;

                using (var context = new Entities())
                {
                    // Ищем пользователя в базе данных
                    var user = context.Пользователи
                        .Include("Роли")
                        .FirstOrDefault(u => u.Логин == login && u.Пароль == password);

                    if (user != null)
                    {
                        _mainWindow.LoginUser(user);
                    }
                    else
                    {
                        _mainWindow.ShowError("Неверный логин или пароль. Проверьте введённые данные и попробуйте снова.",
                                            "Ошибка авторизации");
                        PasswordBox.Password = "";
                        PasswordBox.Focus();
                    }
                }
            }
            catch (System.Exception ex)
            {
                _mainWindow.ShowError($"Ошибка подключения к базе данных: {ex.Message}\nПожалуйста, попробуйте позже.",
                                    "Ошибка соединения");
            }
            finally
            {
                // Восстанавливаем кнопки
                LoginButton.IsEnabled = true;
                GuestButton.IsEnabled = true;
            }
        }

        private void LoginTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Можно добавить дополнительную логику при изменении текста
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Можно добавить дополнительную логику при изменении пароля
        }

        // Обработка нажатия Enter в поле логина
        private void LoginTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Focus();
            }
        }

        // Обработка нажатия Enter в поле пароля
        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AttemptLogin();
            }
        }
    }
}