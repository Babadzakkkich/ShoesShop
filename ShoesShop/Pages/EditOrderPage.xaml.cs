using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ShoesShop.Pages
{
    public partial class EditOrderPage : Page
    {
        private MainWindow _mainWindow;
        private Заказы _currentOrder;
        private bool _isEditMode = false;
        private List<OrderDetailWrapper> _orderItems = new List<OrderDetailWrapper>();

        // Класс-обертка для деталей заказа с вычисляемыми свойствами
        public class OrderDetailWrapper : INotifyPropertyChanged
        {
            public Детали_заказа ДетальЗаказа { get; set; }

            // Вычисляемые свойства
            public decimal TotalPrice => ДетальЗаказа.Количество_товара * ДетальЗаказа.Цена_на_момент_заказа;

            // Прокси-свойства
            public int ID => ДетальЗаказа.ID;
            public int ID_товара => ДетальЗаказа.ID_товара;

            public int Количество_товара
            {
                get => ДетальЗаказа.Количество_товара;
                set
                {
                    if (ДетальЗаказа.Количество_товара != value)
                    {
                        ДетальЗаказа.Количество_товара = value;
                        OnPropertyChanged(nameof(Количество_товара));
                        OnPropertyChanged(nameof(TotalPrice)); // Уведомляем об изменении суммы
                    }
                }
            }

            public decimal Цена_на_момент_заказа => ДетальЗаказа.Цена_на_момент_заказа;
            public Товары Товары => ДетальЗаказа.Товары;

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public EditOrderPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoadData();
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        public EditOrderPage(MainWindow mainWindow, Заказы order)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _currentOrder = order;
            _isEditMode = true;

            LoadData();
            FillForm();

            PageTitle.Text = "Редактирование заказа";
            DeleteButton.Visibility = Visibility.Visible;
        }

        private void LoadData()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    // Загрузка пользователей (только авторизированных клиентов)
                    UserComboBox.ItemsSource = context.Пользователи
                        .Include(u => u.Роли)
                        .Where(u => u.Роли.Роль == "Авторизированный клиент")
                        .ToList();

                    // Загрузка статусов
                    StatusComboBox.ItemsSource = context.Статусы_заказа.ToList();

                    // Загрузка пунктов выдачи
                    PickupPointComboBox.ItemsSource = context.Пункты_выдачи.ToList();

                    if (_isEditMode)
                    {
                        // Загрузка деталей заказа для редактирования
                        var order = context.Заказы
                            .Include(o => o.Детали_заказа.Select(d => d.Товары))
                            .FirstOrDefault(o => o.ID == _currentOrder.ID);

                        if (order != null && order.Детали_заказа != null)
                        {
                            _orderItems = order.Детали_заказа
                                .Select(d => new OrderDetailWrapper { ДетальЗаказа = d })
                                .ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void FillForm()
        {
            if (_currentOrder == null) return;

            // Устанавливаем выбранные значения
            UserComboBox.SelectedValue = _currentOrder.ID_пользователя;
            StatusComboBox.SelectedValue = _currentOrder.ID_статуса;
            PickupPointComboBox.SelectedValue = _currentOrder.ID_пункта_выдачи;

            // Даты
            OrderDatePicker.SelectedDate = _currentOrder.Дата_заказа;
            DeliveryDatePicker.SelectedDate = _currentOrder.Дата_доставки;

            // Код
            CodeTextBox.Text = _currentOrder.Код_для_получения.ToString();

            // Обновляем список товаров
            UpdateOrderItemsList();
            UpdateTotalSum();
        }

        private void UpdateOrderItemsList()
        {
            OrderItemsControl.ItemsSource = null;
            OrderItemsControl.ItemsSource = _orderItems;

            // Подписываемся на изменения
            foreach (var item in _orderItems)
            {
                item.PropertyChanged += OrderItem_PropertyChanged;
            }
        }

        private void OrderItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderDetailWrapper.TotalPrice) ||
                e.PropertyName == nameof(OrderDetailWrapper.Количество_товара))
            {
                UpdateTotalSum();
            }
        }

        private void UpdateTotalSum()
        {
            decimal totalSum = _orderItems.Sum(item => item.TotalPrice);
            TotalSumText.Text = $"{totalSum:N2} руб.";
        }

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем ID уже добавленных товаров
            var existingProductIds = _orderItems.Select(i => i.ID_товара).ToList();

            // Открываем окно выбора товаров
            var selectProductWindow = new SelectProductsWindow(_mainWindow, existingProductIds);
            selectProductWindow.Owner = Window.GetWindow(this);

            if (selectProductWindow.ShowDialog() == true)
            {
                using (var context = new ShoesShopEntities())
                {
                    foreach (var kvp in selectProductWindow.SelectedProductsWithQuantity)
                    {
                        var productId = kvp.Key;
                        var quantity = kvp.Value;

                        var product = context.Товары.Find(productId);
                        if (product != null)
                        {
                            // Проверяем, не добавлен ли уже товар
                            if (!existingProductIds.Contains(productId))
                            {
                                var orderItem = new Детали_заказа
                                {
                                    ID_товара = productId,
                                    Количество_товара = quantity,
                                    Цена_на_момент_заказа = product.Цена
                                };

                                _orderItems.Add(new OrderDetailWrapper { ДетальЗаказа = orderItem });
                            }
                        }
                    }
                }

                UpdateOrderItemsList();
                UpdateTotalSum();
            }
        }

        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                int itemId = (int)button.Tag;
                var itemToRemove = _orderItems.FirstOrDefault(i => i.ID == itemId);

                if (itemToRemove != null)
                {
                    // Отписываемся от событий
                    itemToRemove.PropertyChanged -= OrderItem_PropertyChanged;

                    _orderItems.Remove(itemToRemove);
                    UpdateOrderItemsList();
                    UpdateTotalSum();
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                using (var context = new ShoesShopEntities())
                {
                    Заказы order;

                    if (_isEditMode)
                    {
                        // Редактирование существующего заказа
                        order = context.Заказы
                            .Include(o => o.Детали_заказа)
                            .FirstOrDefault(o => o.ID == _currentOrder.ID);

                        if (order == null)
                        {
                            _mainWindow.ShowError("Заказ не найден в базе данных");
                            return;
                        }

                        // Удаляем старые детали
                        foreach (var oldItem in order.Детали_заказа.ToList())
                        {
                            context.Детали_заказа.Remove(oldItem);
                        }
                        context.SaveChanges(); // Сохраняем удаление старых деталей
                    }
                    else
                    {
                        // Добавление нового заказа
                        order = new Заказы();
                        context.Заказы.Add(order);
                    }

                    // Обновляем данные заказа
                    UpdateOrderFromForm(order);
                    context.SaveChanges();

                    // Добавляем новые детали
                    foreach (var wrapper in _orderItems)
                    {
                        var detail = new Детали_заказа
                        {
                            ID_заказа = order.ID,
                            ID_товара = wrapper.ID_товара,
                            Количество_товара = wrapper.Количество_товара,
                            Цена_на_момент_заказа = wrapper.Цена_на_момент_заказа
                        };
                        context.Детали_заказа.Add(detail);
                    }

                    context.SaveChanges();

                    _mainWindow.ShowMessage(_isEditMode ? "Заказ успешно обновлен!" : "Заказ успешно добавлен!", "Успех");

                    // Возврат на страницу заказов
                    NavigationService.Navigate(new OrdersPage(_mainWindow));
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка при сохранении: {ex.Message}\n\nДетали: {ex.InnerException?.Message}");
            }
        }

        private void UpdateOrderFromForm(Заказы order)
        {
            order.Дата_заказа = OrderDatePicker.SelectedDate ?? DateTime.Now;
            order.Дата_доставки = DeliveryDatePicker.SelectedDate ?? DateTime.Now.AddDays(7);
            order.Код_для_получения = int.Parse(CodeTextBox.Text);

            if (UserComboBox.SelectedItem is Пользователи selectedUser)
                order.ID_пользователя = selectedUser.ID;

            if (StatusComboBox.SelectedItem is Статусы_заказа selectedStatus)
                order.ID_статуса = selectedStatus.ID;

            if (PickupPointComboBox.SelectedItem is Пункты_выдачи selectedPickupPoint)
                order.ID_пункта_выдачи = selectedPickupPoint.ID;
        }

        private bool ValidateForm()
        {
            if (UserComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите пользователя");
                UserComboBox.Focus();
                return false;
            }

            if (StatusComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите статус заказа");
                StatusComboBox.Focus();
                return false;
            }

            if (PickupPointComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите пункт выдачи");
                PickupPointComboBox.Focus();
                return false;
            }

            if (OrderDatePicker.SelectedDate == null)
            {
                _mainWindow.ShowError("Укажите дату заказа");
                OrderDatePicker.Focus();
                return false;
            }

            if (DeliveryDatePicker.SelectedDate == null)
            {
                _mainWindow.ShowError("Укажите дату доставки");
                DeliveryDatePicker.Focus();
                return false;
            }

            if (DeliveryDatePicker.SelectedDate < OrderDatePicker.SelectedDate)
            {
                _mainWindow.ShowError("Дата доставки не может быть раньше даты заказа");
                DeliveryDatePicker.Focus();
                return false;
            }

            if (!int.TryParse(CodeTextBox.Text, out int code) || code <= 0)
            {
                _mainWindow.ShowError("Введите корректный код для получения (положительное целое число)");
                CodeTextBox.Focus();
                CodeTextBox.SelectAll();
                return false;
            }

            if (_orderItems.Count == 0)
            {
                _mainWindow.ShowError("Добавьте хотя бы один товар в заказ");
                AddItemButton.Focus();
                return false;
            }

            // Проверяем, что все количества корректны
            foreach (var item in _orderItems)
            {
                if (item.Количество_товара <= 0)
                {
                    _mainWindow.ShowError($"У товара '{item.Товары?.Наименование_товара}' должно быть положительное количество");
                    return false;
                }
            }

            return true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentOrder == null) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот заказ?\n\n" +
                                        "Все детали заказа также будут удалены.",
                                        "Подтверждение удаления",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new ShoesShopEntities())
                    {
                        // Загружаем заказ со всеми деталями
                        var order = context.Заказы
                            .Include(o => o.Детали_заказа)
                            .FirstOrDefault(o => o.ID == _currentOrder.ID);

                        if (order != null)
                        {
                            // Удаляем заказ (детали удалятся каскадно)
                            context.Заказы.Remove(order);
                            context.SaveChanges();

                            _mainWindow.ShowMessage("Заказ успешно удален!", "Успех");

                            NavigationService.Navigate(new OrdersPage(_mainWindow));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _mainWindow.ShowError($"Ошибка при удалении: {ex.Message}");
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new OrdersPage(_mainWindow));
        }
    }
}