using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Data.Entity;
using System.Windows.Controls;

namespace ShoesShop.Pages
{
    public partial class SelectProductsWindow : Window
    {
        private List<ProductSelectionItem> _allProducts;
        private List<int> _excludedProductIds;
        public Dictionary<int, int> SelectedProductsWithQuantity { get; private set; } // ID товара -> количество

        public SelectProductsWindow(MainWindow mainWindow, List<int> excludedProductIds = null)
        {
            InitializeComponent();
            Owner = mainWindow;

            _excludedProductIds = excludedProductIds ?? new List<int>();
            SelectedProductsWithQuantity = new Dictionary<int, int>();

            LoadProducts();
        }

        // Класс для отображения товара с флажком выбора и количеством
        public class ProductSelectionItem
        {
            public Товары Товар { get; set; }
            public bool IsSelected { get; set; }
            public int Quantity { get; set; }

            // Прокси-свойства для удобства привязки
            public int ID => Товар.ID;
            public string Наименование_товара => Товар.Наименование_товара;
            public string Описание_товара => Товар.Описание_товара;
            public decimal Цена => Товар.Цена;
            public int Количество_на_складе => Товар.Количество_на_складе;
            public string Артикул => Товар.Артикул;
            public string Категория => Товар.Категории?.Категория ?? "Не указана";
            public string Производитель => Товар.Производители?.Производитель ?? "Не указан";

            // Вычисляемые свойства
            public decimal TotalPrice => Цена * Quantity;
            public bool IsQuantityValid => Quantity > 0 && Quantity <= Количество_на_складе;
        }

        private void LoadProducts()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    // Загружаем все товары с категориями и производителями
                    var products = context.Товары
                        .Include(p => p.Категории)
                        .Include(p => p.Производители)
                        .Where(p => !_excludedProductIds.Contains(p.ID)) // Исключаем уже добавленные товары
                        .Where(p => p.Количество_на_складе > 0) // Только товары в наличии
                        .OrderBy(p => p.Наименование_товара)
                        .ToList();

                    // Преобразуем в список с флажками выбора
                    _allProducts = products.Select(p => new ProductSelectionItem
                    {
                        Товар = p,
                        IsSelected = false,
                        Quantity = 1 // По умолчанию 1 штука
                    }).ToList();

                    // Устанавливаем источник данных
                    ProductsItemsControl.ItemsSource = _allProducts;

                    UpdateWindowTitle();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allProducts == null) return;

            var searchText = SearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Показываем все товары
                ProductsItemsControl.ItemsSource = _allProducts;
            }
            else
            {
                // Фильтруем по поисковому запросу
                var filteredProducts = _allProducts.Where(p =>
                    p.Наименование_товара.ToLower().Contains(searchText) ||
                    p.Описание_товара?.ToLower().Contains(searchText) == true ||
                    p.Артикул?.ToLower().Contains(searchText) == true ||
                    p.Категория.ToLower().Contains(searchText) ||
                    p.Производитель.ToLower().Contains(searchText))
                    .ToList();

                ProductsItemsControl.ItemsSource = filteredProducts;
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем валидность введенных количеств
            var invalidItems = _allProducts
                .Where(p => p.IsSelected && !p.IsQuantityValid)
                .ToList();

            if (invalidItems.Any())
            {
                string errorMessage = "Некорректное количество у следующих товаров:\n\n";
                foreach (var item in invalidItems)
                {
                    errorMessage += $"- {item.Наименование_товара}: доступно {item.Количество_на_складе} шт.\n";
                }

                MessageBox.Show(errorMessage, "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Получаем выбранные товары с количествами
            SelectedProductsWithQuantity = _allProducts
                .Where(p => p.IsSelected)
                .ToDictionary(p => p.ID, p => p.Quantity);

            if (SelectedProductsWithQuantity.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один товар", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateWindowTitle()
        {
            int availableCount = _allProducts?.Count ?? 0;
            int inStockCount = _allProducts?.Count(p => p.Количество_на_складе > 0) ?? 0;
            Title = $"Выбор товаров (всего: {availableCount}, в наличии: {inStockCount})";
        }
    }
}