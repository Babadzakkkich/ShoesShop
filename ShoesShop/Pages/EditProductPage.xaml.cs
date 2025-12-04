using Microsoft.Win32;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ShoesShop.Pages
{
    public partial class EditProductPage : Page
    {
        private MainWindow _mainWindow;
        private Товары _currentProduct;
        private bool _isEditMode = false;
        private string _selectedImagePath;
        private BitmapImage _currentImage;

        public EditProductPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            LoadData();
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        public EditProductPage(MainWindow mainWindow, Товары product)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _currentProduct = product;
            _isEditMode = true;

            LoadData();
            FillForm();

            PageTitle.Text = "Редактирование товара";
            DeleteButton.Visibility = Visibility.Visible;
        }

        private void LoadData()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    // Загрузка категорий
                    CategoryComboBox.ItemsSource = context.Категории.ToList();

                    // Загрузка производителей
                    ManufacturerComboBox.ItemsSource = context.Производители.ToList();

                    // Загрузка поставщиков
                    SupplierComboBox.ItemsSource = context.Поставщики.ToList();
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void FillForm()
        {
            if (_currentProduct == null) return;

            ArticleTextBox.Text = _currentProduct.Артикул;
            NameTextBox.Text = _currentProduct.Наименование_товара;
            DescriptionTextBox.Text = _currentProduct.Описание_товара;
            PriceTextBox.Text = _currentProduct.Цена.ToString();
            UnitTextBox.Text = _currentProduct.Единица_измерения;
            StockTextBox.Text = _currentProduct.Количество_на_складе.ToString();
            DiscountTextBox.Text = _currentProduct.Действующая_скидка?.ToString() ?? "0";
            Discount2TextBox.Text = _currentProduct.Скидка?.ToString() ?? "0";

            // Устанавливаем выбранные значения
            CategoryComboBox.SelectedValue = _currentProduct.ID_категории;
            ManufacturerComboBox.SelectedValue = _currentProduct.ID_производителя;
            SupplierComboBox.SelectedValue = _currentProduct.ID_поставщика;

            // Загружаем изображение если есть
            LoadProductImage();
        }

        private void LoadProductImage()
        {
            if (_currentProduct == null) return;

            if (!string.IsNullOrEmpty(_currentProduct.Фото))
            {
                string projectRoot = GetProjectRoot();
                string imagePath = Path.Combine(projectRoot, "Images", _currentProduct.Фото);

                if (File.Exists(imagePath))
                {
                    LoadImageFromPath(imagePath);
                    ImagePathText.Text = Path.GetFileName(imagePath);
                }
                else
                {
                    ImagePathText.Text = "Изображение не найдено";
                }
            }
            else
            {
                ImagePathText.Text = "Изображение не выбрано";
            }
        }

        private void LoadImageFromPath(string imagePath)
        {
            try
            {
                _selectedImagePath = imagePath;
                _currentImage = new BitmapImage();
                _currentImage.BeginInit();
                _currentImage.CacheOption = BitmapCacheOption.OnLoad;
                _currentImage.UriSource = new Uri(imagePath);
                _currentImage.EndInit();

                ProductImage.Source = _currentImage;
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки изображения: {ex.Message}");
            }
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Изображения (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg; *.jpeg; *.png; *.bmp|Все файлы (*.*)|*.*",
                Title = "Выберите изображение для товара"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImageFromPath(openFileDialog.FileName);
                ImagePathText.Text = Path.GetFileName(openFileDialog.FileName);
            }
        }

        private void ClearImageButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedImagePath = null;
            _currentImage = null;
            ProductImage.Source = null;
            ImagePathText.Text = "Изображение не выбрано";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                using (var context = new ShoesShopEntities())
                {
                    Товары product;

                    if (_isEditMode)
                    {
                        // Редактирование существующего товара
                        product = context.Товары.Find(_currentProduct.ID);
                        if (product == null)
                        {
                            _mainWindow.ShowError("Товар не найден в базе данных");
                            return;
                        }
                    }
                    else
                    {
                        // Добавление нового товара
                        product = new Товары();
                        context.Товары.Add(product);
                    }

                    UpdateProductFromForm(product);

                    // Сохраняем изображение
                    if (!string.IsNullOrEmpty(_selectedImagePath) && !string.IsNullOrEmpty(Path.GetFileName(_selectedImagePath)))
                    {
                        SaveImageForProduct(product.ID, _selectedImagePath);
                        product.Фото = Path.GetFileName(_selectedImagePath);
                    }

                    context.SaveChanges();

                    _mainWindow.ShowMessage(_isEditMode ? "Товар успешно обновлен!" : "Товар успешно добавлен!", "Успех");

                    // Возврат на страницу товаров
                    NavigationService.Navigate(new ProductsPage(_mainWindow));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"Внутренняя ошибка: {ex.InnerException?.Message}");
                _mainWindow.ShowError($"Ошибка при сохранении: {ex.Message}\n\nДетали: {ex.InnerException?.Message}");
            }
        }

        private void SaveImageForProduct(int productId, string sourceImagePath)
        {
            try
            {
                string projectRoot = GetProjectRoot();
                string imagesDirectory = Path.Combine(projectRoot, "Images");

                // Создаем папку если не существует
                if (!Directory.Exists(imagesDirectory))
                {
                    Directory.CreateDirectory(imagesDirectory);
                }

                // Генерируем имя файла
                string fileName = $"{productId}_{Path.GetFileNameWithoutExtension(sourceImagePath)}{Path.GetExtension(sourceImagePath)}";
                string targetImagePath = Path.Combine(imagesDirectory, fileName);

                // Копируем/перезаписываем изображение
                File.Copy(sourceImagePath, targetImagePath, true);
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка при сохранении изображения: {ex.Message}", "Внимание");
            }
        }

        private void UpdateProductFromForm(Товары product)
        {
            product.Артикул = ArticleTextBox.Text.Trim();
            product.Наименование_товара = NameTextBox.Text.Trim();
            product.Описание_товара = DescriptionTextBox.Text.Trim();
            product.Цена = decimal.Parse(PriceTextBox.Text);
            product.Единица_измерения = UnitTextBox.Text.Trim();
            product.Количество_на_складе = int.Parse(StockTextBox.Text);

            // Поля скидок
            if (int.TryParse(DiscountTextBox.Text, out int действующаяСкидка))
                product.Действующая_скидка = действующаяСкидка;
            else
                product.Действующая_скидка = null;

            if (int.TryParse(Discount2TextBox.Text, out int скидка))
                product.Скидка = скидка;
            else
                product.Скидка = null;

            // Внешние ключи
            if (CategoryComboBox.SelectedItem is Категории selectedCategory)
                product.ID_категории = selectedCategory.ID;

            if (ManufacturerComboBox.SelectedItem is Производители selectedManufacturer)
                product.ID_производителя = selectedManufacturer.ID;

            if (SupplierComboBox.SelectedItem is Поставщики selectedSupplier)
                product.ID_поставщика = selectedSupplier.ID;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                _mainWindow.ShowError("Введите наименование товара");
                NameTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(PriceTextBox.Text, out decimal price) || price < 0)
            {
                _mainWindow.ShowError("Введите корректную цену (неотрицательное число)");
                PriceTextBox.Focus();
                PriceTextBox.SelectAll();
                return false;
            }

            if (!int.TryParse(StockTextBox.Text, out int stock) || stock < 0)
            {
                _mainWindow.ShowError("Введите корректное количество (неотрицательное целое число)");
                StockTextBox.Focus();
                StockTextBox.SelectAll();
                return false;
            }

            // Проверка действующей скидки
            if (!string.IsNullOrEmpty(DiscountTextBox.Text))
            {
                if (!int.TryParse(DiscountTextBox.Text, out int discount) || discount < 0 || discount > 100)
                {
                    _mainWindow.ShowError("Введите корректную действующую скидку (число от 0 до 100)");
                    DiscountTextBox.Focus();
                    DiscountTextBox.SelectAll();
                    return false;
                }
            }

            // Проверка скидки
            if (!string.IsNullOrEmpty(Discount2TextBox.Text))
            {
                if (!int.TryParse(Discount2TextBox.Text, out int discount) || discount < 0 || discount > 100)
                {
                    _mainWindow.ShowError("Введите корректную скидку (число от 0 до 100)");
                    Discount2TextBox.Focus();
                    Discount2TextBox.SelectAll();
                    return false;
                }
            }

            if (CategoryComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите категорию");
                CategoryComboBox.Focus();
                return false;
            }

            if (ManufacturerComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите производителя");
                ManufacturerComboBox.Focus();
                return false;
            }

            if (SupplierComboBox.SelectedItem == null)
            {
                _mainWindow.ShowError("Выберите поставщика");
                SupplierComboBox.Focus();
                return false;
            }

            return true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProduct == null) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот товар?\n\n" +
                                        "Примечание: Товар не может быть удален, если он содержится в существующих заказах.",
                                        "Подтверждение удаления",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new ShoesShopEntities())
                    {
                        // Загружаем товар со всеми зависимостями
                        var product = context.Товары
                            .Include(t => t.Детали_заказа)
                            .FirstOrDefault(p => p.ID == _currentProduct.ID);

                        if (product != null)
                        {
                            // Проверяем, есть ли товар в заказах
                            if (product.Детали_заказа.Any())
                            {
                                _mainWindow.ShowError("Невозможно удалить товар, так как он содержится в существующих заказах.\n" +
                                                     "Сначала удалите связанные заказы или детали заказов.",
                                                     "Ошибка удаления");
                                return;
                            }

                            // Удаляем товар
                            context.Товары.Remove(product);
                            context.SaveChanges();

                            _mainWindow.ShowMessage("Товар успешно удален!", "Успех");

                            NavigationService.Navigate(new ProductsPage(_mainWindow));
                        }
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    // Обработка ошибок БД
                    string errorMessage = "Ошибка при удалении товара.\n";

                    if (dbEx.InnerException != null)
                    {
                        if (dbEx.InnerException.Message.Contains("REFERENCE constraint"))
                        {
                            errorMessage += "Товар связан с другими записями в базе данных (возможно, с заказами).\n" +
                                           "Сначала удалите связанные записи.";
                        }
                        else
                        {
                            errorMessage += dbEx.InnerException.Message;
                        }
                    }

                    _mainWindow.ShowError(errorMessage);
                }
                catch (Exception ex)
                {
                    _mainWindow.ShowError($"Ошибка при удалении: {ex.Message}");
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ProductsPage(_mainWindow));
        }

        private string GetProjectRoot()
        {
            string projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)
                                         ?.Parent?.Parent?.FullName;

            if (string.IsNullOrEmpty(projectRoot))
            {
                projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            }

            return projectRoot;
        }
    }
}