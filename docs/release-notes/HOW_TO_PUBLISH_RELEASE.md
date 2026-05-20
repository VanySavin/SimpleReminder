# Как выложить Release на GitHub

Архив для v1.0.0 уже собран локально:

`publish\SimpleReminder-v1.0.0-win-x64-portable.zip` (~63 МБ)

## Вариант A — GitHub CLI (рекомендуется)

```powershell
cd c:\YandexDisk\winproject
gh auth login
.\scripts\create-github-release.ps1
```

Скрипт создаст тег `v1.0.0`, Release и прикрепит zip.

## Вариант B — через сайт GitHub

1. Откройте [Releases → Draft a new release](https://github.com/VanySavin/SimpleReminder/releases/new)
2. **Choose a tag:** `v1.0.0` → **Create new tag** на коммите `main`
3. **Release title:** `SimpleReminder v1.0.0`
4. Описание — скопируйте из [`v1.0.0.md`](v1.0.0.md)
5. Прикрепите файл `publish\SimpleReminder-v1.0.0-win-x64-portable.zip`
6. **Publish release**

После публикации ссылка «Скачать» в README будет вести на актуальный релиз.
