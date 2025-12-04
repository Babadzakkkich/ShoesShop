using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace ShoesShop.Pages
{
    public partial class OrdersPage : Page
    {
        private MainWindow _mainWindow;
        private List<Заказы> _orders;
        private bool _isAdmin = false;
        private bool _isManager = false;

        // Класс-обертка для заказа с вычисляемыми свойствами
        public class OrderWrapper
        {
            private readonly Заказы _order;

            public OrderWrapper(Заказы order)
            {
                _order = order;
            }

            // Прокси-свойства для доступа к данным заказа
            public int ID => _order.ID;
            public DateTime Дата_заказа => _order.Дата_заказа;
            public DateTime Дата_доставки => _order.Дата_доставки;
            public int Код_для_получения => _order.Код_для_получения;
            public Статусы_заказа Статусы_заказа => _order.Статусы_заказа;
            public Пункты_выдачи Пункты_выдачи => _order.Пункты_выдачи;
            public Пользователи Пользователи => _order.Пользователи;
            public ICollection<Детали_заказа> Детали_заказа => _order.Детали_заказа;

            // Вычисляемые свойства
            public decimal TotalOrderSum
            {
                get
                {
                    if (_order.Детали_заказа == null) return 0;
                    return _order.Детали_заказа.Sum(d =>
                        d.Количество_товара * d.Цена_на_момент_заказа);
                }
            }

            // Полное название статуса
            public string StatusFullName
            {
                get
                {
                    if (_order.Статусы_заказа == null) return "Не определен";
                    return _order.Статусы_заказа.Статус;
                }
            }

            // Форматированная дата заказа
            public string FormattedOrderDate => Дата_заказа.ToString("dd.MM.yyyy HH:mm");

            // Форматированная дата доставки
            public string FormattedDeliveryDate => Дата_доставки.ToString("dd.MM.yyyy");
        }

        public OrdersPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            Loaded += OrdersPage_Loaded;
        }

        private void OrdersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadOrders();
            SetupUserPermissions();
        }

        private void SetupUserPermissions()
        {
            if (_mainWindow.CurrentUser != null)
            {
                var role = _mainWindow.CurrentUser.Роли?.Роль;
                _isAdmin = role == "Администратор";
                _isManager = role == "Менеджер";

                // Показываем кнопку добавления только для администратора
                AddOrderButton.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                AddOrderButton.Visibility = Visibility.Collapsed;
            }
        }

        // Класс для расширения деталей заказа
        public class OrderDetailWrapper
        {
            private readonly Детали_заказа _detail;

            public OrderDetailWrapper(Детали_заказа detail)
            {
                _detail = detail;
            }

            public Детали_заказа Detail => _detail;

            public decimal TotalPrice
            {
                get
                {
                    return _detail.Количество_товара * _detail.Цена_на_момент_заказа;
                }
            }
        }

        private void LoadOrders()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    // Загружаем все заказы с связанными данными ОДНИМ запросом
                    _orders = context.Заказы
                        .Include(z => z.Статусы_заказа)
                        .Include(z => z.Пункты_выдачи)
                        .Include(z => z.Пользователи)
                        .Include(z => z.Детали_заказа.Select(d => d.Товары)) // Загружаем детали с товарами
                        .OrderByDescending(z => z.Дата_заказа)
                        .ToList();

                    // Создаем обертки для заказов
                    var orderWrappers = _orders.Select(order => new OrderWrapper(order)).ToList();
                    OrdersItemsControl.ItemsSource = orderWrappers;

                    NoOrdersText.Visibility = orderWrappers.Any() ? Visibility.Collapsed : Visibility.Visible;

                    // Отладочная информация
                    Console.WriteLine($"Загружено заказов: {_orders.Count}");
                    foreach (var order in _orders)
                    {
                        Console.WriteLine($"Заказ {order.ID}: {order.Детали_заказа?.Count ?? 0} деталей");
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки заказов: {ex.Message}\n{ex.InnerException?.Message}");
            }
        }

        // Обработчик клика по карточке заказа
        private void OrderBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((_isAdmin || _isManager) && sender is Border border && border.Tag != null)
            {
                int orderId = (int)border.Tag;
                var order = _orders.FirstOrDefault(o => o.ID == orderId);

                if (order != null)
                {
                    if (_isAdmin)
                    {
                        // Переходим на страницу редактирования заказа для администратора
                        NavigationService.Navigate(new EditOrderPage(_mainWindow, order));
                    }
                    else if (_isManager)
                    {
                        // Для менеджера показываем информацию о заказе
                        ShowOrderInfo(order);
                    }
                }
            }
        }

        private void ShowOrderInfo(Заказы order)
        {
            string orderInfo = $"Заказ №{order.ID}\n" +
                             $"Статус: {order.Статусы_заказа?.Статус}\n" +
                             $"Дата заказа: {order.Дата_заказа:dd.MM.yyyy HH:mm}\n" +
                             $"Дата доставки: {order.Дата_доставки:dd.MM.yyyy}\n" +
                             $"Пользователь: {order.Пользователи?.ФИО}\n" +
                             $"Код для получения: {order.Код_для_получения}\n" +
                             $"Пункт выдачи: {order.Пункты_выдачи?.Адрес}\n\n" +
                             $"Товары в заказе:";

            if (order.Детали_заказа != null && order.Детали_заказа.Any())
            {
                foreach (var detail in order.Детали_заказа)
                {
                    orderInfo += $"\n  - {detail.Товары?.Наименование_товара}: " +
                               $"{detail.Количество_товара} x {detail.Цена_на_момент_заказа:N2} руб.";
                }
            }
            else
            {
                orderInfo += "\n  Нет товаров";
            }

            _mainWindow.ShowMessage(orderInfo, "Информация о заказе");
        }

        // Обработчик кнопки добавления заказа
        private void AddOrderButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditOrderPage(_mainWindow));
        }
    }
}