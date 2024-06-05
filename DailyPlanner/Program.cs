using System;
using Npgsql;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
}

public class Task
{
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
    
    public bool RegisterUser(string username, string password)
    {
        using var connection = GetConnection();
        connection.Open();

        using var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM dailyplanner.Users WHERE Username = @username", connection);
        checkCommand.Parameters.AddWithValue("@username", username);
        if ((long)checkCommand.ExecuteScalar() > 0)
        {
            Console.WriteLine("Пользователь с таким именем уже существует.");
            return false;
        }

        var hashedPassword = HashPassword(password);

        using var command = new NpgsqlCommand("INSERT INTO dailyplanner.Users (Username, PasswordHash) VALUES (@username, @passwordHash)", connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@passwordHash", hashedPassword);
        command.ExecuteNonQuery();

        Console.WriteLine("Пользователь успешно зарегистрирован.");
        return true;
    }
    
    public User? AuthenticateUser(string username, string password)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = new NpgsqlCommand("SELECT UserId, Username, PasswordHash FROM dailyplanner.Users WHERE Username = @username", connection);
        command.Parameters.AddWithValue("@username", username);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var storedHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
            if (VerifyPasswordHash(password, storedHash))
            {
                return new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = storedHash
                };
            }
        }

        Console.WriteLine("Неверное имя пользователя или пароль.");
        return null;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
    }

    private bool VerifyPasswordHash(string password, string storedHash)
    {
        var hashedPassword = HashPassword(password);
        return hashedPassword == storedHash;
    }
    
    public void AddTask(int userId, string title, string description, DateTime dueDate)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = new NpgsqlCommand("INSERT INTO dailyplanner.Tasks (UserId, Title, Description, DueDate, IsCompleted) VALUES (@userId, @title, @description, @dueDate, @isCompleted)", connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@description", description);
        command.Parameters.AddWithValue("@dueDate", dueDate);
        command.Parameters.AddWithValue("@isCompleted", false); // По умолчанию задача не выполнена
        command.ExecuteNonQuery();
    }
    
    public void DeleteTask(int userId, int taskId)
    {
        using var connection = GetConnection();
        connection.Open();

        using var command = new NpgsqlCommand("DELETE FROM dailyplanner.Tasks WHERE UserId = @userId AND TaskId = @taskId", connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@taskId", taskId);
        command.ExecuteNonQuery();
    }
    
    public IEnumerable<Task> GetTasksForUser(int userId)
    {
        var tasks = new List<Task>();

        using var connection = GetConnection();
        connection.Open();

        using var command = new NpgsqlCommand("SELECT * FROM dailyplanner.Tasks WHERE UserId = @userId", connection);
        command.Parameters.AddWithValue("@userId", userId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(new Task
            {
                TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
                IsCompleted = reader.GetBoolean(reader.GetOrdinal("IsCompleted"))
            });
        }

        return tasks;
    }
}

class Program
{
    static DatabaseService databaseService = new DatabaseService("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=123456789;");
    
    static void Main(string[] args)
    {
        Console.WriteLine("Добро пожаловать в консольное приложение-ежедневник!");
        bool isRunning = true;
        User? currentUser = null;

        while (isRunning)
        {
            if (currentUser == null)
            {
                Console.WriteLine("1. Регистрация");
                Console.WriteLine("2. Авторизация");
                Console.WriteLine("3. Выход");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        if (RegisterUser())
                        {
                            Console.WriteLine("Регистрация прошла успешно.");
                        }
                        break;
                    case "2":
                        currentUser = AuthenticateUser();
                        break;
                    case "3":
                        isRunning = false;
                        break;
                    default:
                        Console.WriteLine("Неверный ввод. Пожалуйста, выберите действие из списка.");
                        break;
                }
            }
            else
            {
                Console.WriteLine($"Добро пожаловать, {currentUser.Username}!");
                Console.WriteLine("4. Добавить задачу");
                Console.WriteLine("5. Удалить задачу");
                Console.WriteLine("6. Просмотреть задачи");
                Console.WriteLine("7. Выйти из аккаунта");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "4":
                        AddTask(currentUser);
                        break;
                    case "5":
                        DeleteTask(currentUser);
                        break;
                    case "6":
                        ViewTasks(currentUser);
                        break;
                    case "7":
                        currentUser = null;
                        break;
                    default:
                        Console.WriteLine("Неверный ввод. Пожалуйста, выберите действие из списка.");
                        break;
                }
            }
        }
    }
    
    public static bool RegisterUser()
    {
        Console.WriteLine("Введите имя пользователя:");
        string username = Console.ReadLine();
        Console.WriteLine("Введите пароль:");
        string password = Console.ReadLine();

        return databaseService.RegisterUser(username, password);
    }

    public static User? AuthenticateUser()
    {
        Console.WriteLine("Введите имя пользователя:");
        string username = Console.ReadLine();
        Console.WriteLine("Введите пароль:");
        string password = Console.ReadLine();

        return databaseService.AuthenticateUser(username, password);
    }

    public static void AddTask(User user)
    {
        Console.WriteLine("Введите название задачи:");
        string title = Console.ReadLine();
        Console.WriteLine("Введите описание задачи:");
        string description = Console.ReadLine();
        Console.WriteLine("Введите дату завершения задачи (гггг-мм-дд):");
        DateTime dueDate;
        while (!DateTime.TryParse(Console.ReadLine(), out dueDate))
        {
            Console.WriteLine("Неверный формат даты, попробуйте ещё раз (гггг-мм-дд):");
        }

        databaseService.AddTask(user.UserId, title, description, dueDate);
        Console.WriteLine("Задача добавлена.");
    }

    public static void DeleteTask(User user)
    {
        Console.WriteLine("Введите ID задачи для удаления:");
        int taskId;
        while (!int.TryParse(Console.ReadLine(), out taskId))
        {
            Console.WriteLine("Неверный формат ID, попробуйте ещё раз:");
        }

        databaseService.DeleteTask(user.UserId, taskId);
        Console.WriteLine("Задача удалена.");
    }

    public static void ViewTasks(User user)
    {
        var tasks = databaseService.GetTasksForUser(user.UserId);
        if (tasks.Any())
        {
            foreach (var task in tasks)
            {
                Console.WriteLine($"ID: {task.TaskId}, Название: {task.Title}, Описание: {task.Description}, Срок: {task.DueDate.ToShortDateString()}, Выполнено: {task.IsCompleted}");
            }
        }
        else
        {
            Console.WriteLine("У вас нет задач.");
        }
    }
}
