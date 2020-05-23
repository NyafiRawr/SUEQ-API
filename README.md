# Server Universal Electronic Queue
Сервер универсальной электронной очереди - WEB API, архитектура: REST, используемая база данных: MYSQL, авторизация по токену: JWT, формат запросов: JSON, доступен SSL (HTTPS), работает с подтверждениями через почту

Содержание
============

<!--ts-->
   * [Проделанная работа](./README.md#План)
   * [Подготовка базы данных](./README.md#База-данных)
   * [Запуск сервера](./README.md#Запуск)
   * [Логика API с примерами](./README.md#Логика)
	   * [Пользователи](./README.md#Users)
	   * [Очереди](./README.md#Queues)
	   * [Позиции в очередях](./README.md#Positions)
<!--te-->

План
============

- [x] 1. Создан проект WEB-API с ASP.NET (3.1) и настроен вывод при подключении к корню через браузер
- [x] 2. Создана папка `Models` и созданы сущности (таблицы) по схеме. Пакет `Microsoft.EnityFrameworkCore.Tools`
- [x] 3. Создан класс `SUEQContext` в `Models` и указаны сущности, которые он должен создавать
- [x] 4. Через `NuGet` добавлен `Pomelo.EntityFrameworkCore.MySql` для подключения к БД
- [x] 5. Подключен `MySql` и реализована строка подключения в `ConfigureServices` в `Startup.cs`
- [x] 6. Создана папка `Controllers` и контроллер для сущности пользователя, протестировано с помощью Postman
- [x] 7. Изучены варианты авторизации по звонку, смс и Google+
- [x] 8. Разбор `https` и попытки внедрения в проект. Необходимо изучить сертификаты. Работает через `VS`: нужно открыть `Свойства проекта` - `Отладка` и в самом низу `Включить SSL`, а затем в `appsettings.json` изменить поле `https_port` на порт из свойств проекта (`0` - выключено).
- [x] 9. Изучены варианты и виды аутентификации и примерные варианты реализации
- [x] 11. Все настройки вынесены в `appsettings.JSON` и правильно переподключены к проекту
- [x] 12. Небольшое логгирование включения `SSL` и обращения к корню
- [x] 13. Реализовано хэширование пароля с солью и валидация почты
- [x] 14. Проработана логика сервера (изменялась более чем несколько раз)
- [x] 15. Реализована работа с токеном `JWT`, передаётся как `Bearer` при запросах. Пакет `Microsoft.AspNetCore.Authentication.JwtBearer`
- [x] 16. Проработана регистрация и авторизация, протестирована работа токена
- [x] 17. Реализован контроллер управления пользователем (получение, обновление информации и удаление аккаунта)
- [x] 18. Реализован контроллер управления очередями
- [x] 19. Реализован контроллер управления позициями
- [x] 20. Полное тестирование, попытки отсылать неправильные запросы, осмотр ответов. Итог: п.21 и п.22
- [x] 21. Внедрение рефреш токена
- [x] 22. Изменить ответы запросов на постоянную форму
- [x] 23. Добавлено подтверждение регистрации и смена пароля по почте. Пакет `Sendgrid v6.3.4` (SMTP)
- [x] 24. Доработка `HTTPS` (п.8), работа с `.PFX` сертификатом с паролем. Пакет `Microsoft.AspNetCore.Authentication.Certificate`
- [x] 25. `Deep Linking` и QR-код. Пакет `QRCoder`
- [x] 26. Защита от множественных обращений - ReCaptcha от Google (отключена, так как нужна реализация ответа с front-end). Пакет `Microsoft.AspNetCore.WebPages`   
  
База данных
============

Создание базы данных и предоставление доступа:  
```mysql
CREATE DATABASE DBNAMEHERE;
CREATE USER 'USERNAMEHERE'@'%' IDENTIFIED BY 'USERPASSWORDHERE';
GRANT ALL PRIVILEGES ON DBNAMEHERE.* TO 'USERNAMEHERE'@'%';
FLUSH PRIVILEGES;
```
  
Запуск
============
  
Для запуска `SUEQ-API.exe` нужно заполнить `empty_appsettings.json` своими данными подключений и переименовать его в `appsettings.json`!  
  
Логика
============

## Users
  
### Регистрация (с подтверждением почты)  
http://localhost:5433/api/users/registration `POST`
  
### Авторизация (первое получение токена доступа и токена обновления)  
http://localhost:5433/api/users/login `GET`  
Все следующие обращения выполняются с этим токеном доступа как `Auth: Bearer Token` когда его срок исткает необходимо обновить токены  
  
### Обновление токенов доступа  
http://localhost:5433/api/users/refresh `POST`
  
### Сброс пароля  
http://localhost:5433/api/users/forgot/password?email=local@host.com  `POST` 
  
### Получение информации о себе  
http://localhost:5433/api/users/info `GET`
  
### Обновление информации о себе  
http://localhost:5433/api/users/update `PUT`  
  
### Удаление пользователем своего аккаунта  
http://localhost:5433/api/users/delete `DELETE`
  
## Queues

### Создать очередь  
http://localhost:5433/api/queues/create `POST`  
QR-код является Bitmap в byte[] и содержит в себе следующую ссылку: `UrlForLinks/api/queues/{QueueId}`  
  
### Изменить название, описание или статус очереди  
http://localhost:5433/api/queues/update/44 {QueueId=44} `PUT`  
  
### Получить информацию об очереди  
http://localhost:5433/api/queues/info/44 {QueueId=44} `GET`  
  
###  Удалить очередь 
http://localhost:5433/api/queues/delete/44 `DELETE`  
  
## Positions

### Встать в очередь  
http://localhost:5433/api/positions/44 `POST`  
  
### Выйти из очереди  
http://localhost:5433/api/positions/44 `DELETE`  
  
### Удалить стоящего в очереди (владелец)  
http://localhost:5433/api/positions/44/7 {UserId=7} `DELETE`  
  
### Изменить позицию стоящего в очереди (владелец)  
http://localhost:5433/api/positions/44 `PUT`
  
### Получение информации о пользователях в очереди
http://localhost:5433/api/positions/44 `GET`  
