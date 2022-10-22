# AVPN-Service

- Описание
  - Служба для автоматического подключения VPN при старте компьютера, без участия пользователя.
- Установка сервиса
  - Основной способ: ключ install
    - AVPN.exe install -vpnname <connection_name> -checkhost <internal_host_to_check_network> -cfgurl http://config.youdomain.local/routes/routes.txt -netsrv true
  - Средствами утилиты SC:
    - sc create AVPN DisplayName="AVPN" binpath="c:\tmp\AVPN.exe -vpnname <connection_name> -checkhost <internal_host_to_check_network> -cfgurl http://config.youdomain.local/routes/routes.txt" start=auto
- Формат файла с маршрутами
  - ```
    [routes]
    10.1.1.0 255.255.255.0 10.1.3.1
    10.2.2.0 255.255.255.0 10.1.3.1
    ```
- Параметры командной строки
  - install
    - Производит регистрацию службы в системе в том месте откуда был запущен исполняемый файл.
    - Всегда должен идти первым
    - Пример:
      - AVPN.exe install -vpnname <connection_name> -checkhost <internal_host_to_check_network> -cfgurl http://config.youdomain.local/routes/routes.txt
  - remove
    - Производит удаление службы из системы
    - Пример:
      - AVPN.exe remove
  - vpnname
    - Задаёт имя VPN подключения для управления им.
    - Будет использоваться адресная книга "C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk"
    - Пример: задать имя подключения "vpntowork"
      - -vpnname vpntowork
  - netsrv true
    - Запуск подключения VPN из-под учётной записи NETWORK SERVICE
    - Будет зарегистрирован дополнительный сервис AVPNnet работающий как NETWORK SERVICE
    - Общение между службами осуществляется через именованный канал
    - Необходимо для авторизации под учётной записью компьютера (computername$)
  - checkhost
    - Задаёт имя хоста для проверки связи (проверка расположения клиента, внутри или снаружи периметра)
    - Если имя резолвится в несколько IP, то они будут проверяться все, пока не встретится доступный, после этого проверка завершится успехом.
    - Если параметр не задан, будет предпринята попытка узнать домен машины. Если машина в домене, то будет использоваться имя домена как хост для проверки.
    - Пример: задать имя хоста для проверки "internalcheck.youdomain.local"
      -  -checkhost internalcheck.youdomain.local
    - Пример: задать IP хоста для проверки "10.10.1.2"
      -  -checkhost 10.10.1.2
  - cfgurl
    - Задаёт URL откуда будет предпринята попытка скачать файл конфигурации (список маршрутов)
    - Пример: -cfgurl http://config.youdomain.local/routes/routes.txt
  - Interval
    - Задаёт периодичность проверки состояния подключения в секундах (и расположения клиента, за или внутри периметра)
    - Если не задано, то устанавливается интервал по умолчанию 10 секунд
    - Пример: установить интервал 15 секунд
      -  -Interval 15
  - user
    - Задаёт логин VPN подключения
    - Не безопасно!
  - pass
    - Задаёт пароль VPN подключения
    - Не безопасно!
  - logpath
    - Задаёт расположение лог-файла
    - Если не задано, логи располагаются в той же директории что и сервис
    - Пример: -logpath %ALLUSERSPROFILE%\VPNSrv\log
  - logsize
    - Задаёт максимальный размер файла лога в килобайтах
    - Если не задано, значение по умолчанию 2MB
    - Пример: установить максимальный размер лога 5МБ
      -  -logsize 5000
			
- Логика работы
  - Запуск
    - Разбор параметров командной строки
    - Запуск цикла проверки
  - Работа (цикл проверки статуса подключения)
    - Проверяем поднято ли VPN подключение, заданное в параметре -vpnname
      - Если да, то проверяем читали ли мы конфигурацию маршрутов
        - Если да, то ничего
        - Если нет, то читаем файл конфигурации маршрутов, заданный параметром -cfgurl
          - ставим флаг, что файл прочитан
          - применяем прочитанные маршруты
      - Если нет, то:
        - ставим флаг что файл маршрутов не прочитан
        - Если есть полученные маршруты, то
          - удаляем маршруты из системы и переменной
        - проверяем доступность хоста, заданного в параметре -checkhost
          - Если хост доступен, то ничего
          - Если нет, поднимаем VPN подключение, заданное в параметре -vpnname
  - Остановка
    - Если есть полученные маршруты, то
      - удаляем маршруты из системы и переменной
