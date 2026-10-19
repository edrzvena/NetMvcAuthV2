# FLOW — Alur Lengkap NetMvcAuthV2

Dokumen ini menjelaskan **cara kerja aplikasi dari ujung ke ujung**: struktur folder, fungsi tiap file, siapa memanggil siapa,
isi `Program.cs` baris demi baris (termasuk lifetime/scope tiap service), dan alur tiap fitur dari browser sampai database.

> Stack: ASP.NET Core MVC (.NET 10) · Entity Framework Core + SQL Server · Razor + Bootstrap 5.3 · Cookie authentication (tanpa ASP.NET Core Identity).
> Spesifikasi produk ada di [`prd.md`](prd.md). Cara menjalankan ada di [`README.md`](README.md).

## Daftar Isi

1. [Gambaran Besar](#1-gambaran-besar)
2. [Peta Folder dan Arti Namanya](#2-peta-folder-dan-arti-namanya)
3. [Siapa Memanggil Siapa (Antar Folder)](#3-siapa-memanggil-siapa-antar-folder)
4. [Penjelasan Setiap File](#4-penjelasan-setiap-file)
5. [Program.cs — Penjelasan Penuh](#5-programcs--penjelasan-penuh)
6. [Alur Setiap Fitur (File ke File)](#6-alur-setiap-fitur-file-ke-file)
7. [View, Layout, dan Aset Statis](#7-view-layout-dan-aset-statis)
8. [Database dan Migration](#8-database-dan-migration)
9. [Konfigurasi dan Secret](#9-konfigurasi-dan-secret)
10. [Keamanan: Fitur Mana Ada di File Mana](#10-keamanan-fitur-mana-ada-di-file-mana)
11. [Testing](#11-testing)
12. [Glosarium Nama Folder dan File](#12-glosarium-nama-folder-dan-file)
13. [Cara Menambah Fitur Baru](#13-cara-menambah-fitur-baru)
14. [Catatan dan Keterbatasan Saat Ini](#14-catatan-dan-keterbatasan-saat-ini)

---

## 1. Gambaran Besar

Aplikasi memakai **arsitektur berlapis** dengan aturan dependensi satu arah:

```text
Browser
   │  HTTP request
   ▼
Middleware pipeline (Program.cs)  ──►  rate limiter → authentication → authorization
   │
   ▼
Controller          (Controllers/)      menerima request, validasi model, memilih View / redirect
   │  memanggil lewat INTERFACE
   ▼
Service             (Services/)         aturan bisnis: hash password, lockout, token reset, sign-in cookie
   │  memanggil lewat INTERFACE
   ▼
Repository          (Repositories/)     akses data: query ke tabel Users
   │
   ▼
DbContext + Model   (Data/, Models/)    EF Core ⇄ SQL Server
```

Pendukung yang dipakai lintas lapisan:

| Pendukung | Folder | Fungsinya |
|---|---|---|
| DTO | `DTOs/Auth/` | Bentuk data masuk dari form + aturan validasi |
| Mapping | `Mappings/` | Mengubah DTO → Model dan Model → claims |
| Constants | `Constants/` | Nilai tetap (nama role, nama cookie, batas lockout, dll.) |
| Common | `Common/` | Enum, pengaturan, dan helper keamanan yang dipakai banyak tempat |

**Aturan yang dijaga:**

- Controller **tidak** memakai `Repositories.*` atau `Data` — hanya `IAuthService`.
- Service **tidak tahu soal View** — hanya mengembalikan `AuthResult(Success, Error)`.
- Repository **tidak berisi aturan bisnis** — hanya query.
- Controller hanya bind ke **DTO**, tidak pernah ke `User`, jadi `Role` tidak bisa diisi dari form (anti over-posting).

---

## 2. Peta Folder dan Arti Namanya

```text
NetMvcAuthV2/
├── Program.cs                      Titik masuk aplikasi: mendaftarkan service & menyusun middleware
├── NetMvcAuthV2.csproj             Definisi project: target net10.0, paket NuGet, UserSecretsId
├── appsettings.json                Konfigurasi umum (tanpa secret)
├── appsettings.Development.json    Konfigurasi tambahan khusus Development
├── prd.md  README.md  FLOW.md      Dokumentasi
│
├── Properties/
│   └── launchSettings.json         Profil menjalankan app (URL, environment Development)
│
├── Common/                         "Umum": hal yang dipakai lintas lapisan, bukan milik satu fitur
│   ├── Enums/UserRole.cs               enum peran: User = 0, Admin = 1
│   ├── Security/SecurityStampCookieValidator.cs   pemeriksa cookie tiap request
│   └── Settings/SmtpSettings.cs        bentuk data konfigurasi section "Smtp"
│
├── Constants/                      "Konstanta": nilai tetap yang tidak boleh berubah saat runtime
│   ├── AuthSettings.cs                 batas password, jumlah gagal login, lama lockout, umur token
│   ├── CookieNames.cs                  nama cookie auth
│   ├── RateLimitPolicies.cs            nama policy rate limiter
│   └── Roles.cs                        nama role sebagai string untuk [Authorize(Roles = ...)]
│
├── Models/                         "Model": representasi data/tabel
│   ├── User.cs                         entity tabel Users
│   └── ErrorViewModel.cs               data untuk halaman error (bawaan template)
│
├── DTOs/Auth/                      "Data Transfer Object": data yang dibawa form antar lapisan
│   ├── RegisterDto.cs
│   ├── LoginDto.cs
│   ├── ForgotPasswordDto.cs
│   └── ResetPasswordDto.cs
│
├── Mappings/
│   └── UserMappings.cs             Konversi DTO → User dan User → ClaimsPrincipal
│
├── Data/                           "Data": segala hal tentang EF Core dan database
│   ├── AppDbContext.cs                 pintu masuk EF Core ke SQL Server
│   ├── Configurations/UserConfiguration.cs   aturan kolom/index tabel Users (Fluent API)
│   └── Seed/DbSeeder.cs                pembuat akun Admin awal
│
├── Migrations/                     "Migrasi": riwayat perubahan skema database (dibuat oleh dotnet ef)
│   ├── 20261019140019_AddUser.cs               perintah Up/Down untuk membuat tabel Users
│   ├── 20261019140019_AddUser.Designer.cs      snapshot model saat migration dibuat (metadata EF)
│   └── AppDbContextModelSnapshot.cs            snapshot model terkini (pembanding migration berikutnya)
│
├── Repositories/                   "Repositori": lapisan akses data
│   ├── Interfaces/{IRepository, IUserRepository}.cs        kontrak
│   └── Implementations/{Repository, UserRepository}.cs     isi
│
├── Services/                       "Layanan": lapisan aturan bisnis
│   ├── Interfaces/{IAuthService, IEmailSender}.cs          kontrak
│   └── Implementations/{AuthService, SmtpEmailSender, FakeEmailSender}.cs
│
├── Controllers/                    "Pengendali": menerima request HTTP
│   ├── AccountController.cs            register, login, logout, lupa & reset password, access denied
│   ├── AdminController.cs              halaman khusus Admin
│   └── HomeController.cs               beranda, privacy, error
│
├── Views/                          "Tampilan": template Razor (.cshtml)
│   ├── _ViewStart.cshtml               menetapkan layout default (_Layout) untuk semua view
│   ├── _ViewImports.cshtml             using & tag helper yang berlaku untuk semua view
│   ├── Account/                        6 halaman auth
│   ├── Admin/Index.cshtml              Panel Admin
│   ├── Home/{Index, Privacy}.cshtml    landing (tamu) / dashboard (login), privacy
│   └── Shared/                         layout, partial, dan halaman error yang dipakai bersama
│
├── wwwroot/                        "Web root": satu-satunya folder yang dilayani langsung ke browser
│   ├── css/site.css                    gaya global
│   ├── js/{auth.js, site.js}           skrip browser
│   ├── img/{logo.svg, logo-light.svg}  logo
│   ├── favicon.ico
│   └── lib/                            bootstrap, jquery, jquery-validation (pustaka pihak ketiga)
│
└── tests/NetMvcAuthV2.Tests/       Project unit test terpisah (xUnit)
    ├── AuthServiceTests.cs
    └── UserMappingsTests.cs
```

Folder `bin/` dan `obj/` adalah hasil build (diabaikan git). Folder `tests/` dikeluarkan dari kompilasi project utama lewat
`<Compile Remove="tests\**" />` di `.csproj`, kalau tidak, file test ikut ter-compile ke aplikasi.

---

## 3. Siapa Memanggil Siapa (Antar Folder)

Tanda `→` = "memakai / merujuk ke".

| Folder | Memakai folder | Untuk apa |
|---|---|---|
| `Program.cs` | semuanya | Mendaftarkan (wiring) semua class ke DI dan menyusun pipeline |
| `Controllers/` | `Services/Interfaces`, `DTOs/Auth`, `Constants` | Memanggil `IAuthService`; menerima DTO; nama policy & role |
| `Services/Implementations` | `Repositories/Interfaces`, `Services/Interfaces`, `Mappings`, `DTOs/Auth`, `Models`, `Constants` | Menjalankan aturan bisnis lewat repository dan mapping |
| `Repositories/Implementations` | `Data`, `Models`, `Repositories/Interfaces` | Query lewat `AppDbContext` |
| `Data/` | `Models`, `Common/Enums` | Memetakan `User` ke tabel (`UserConfiguration`) |
| `Data/Seed` | `Mappings`, `Models`, `Common/Enums` | Membuat user Admin |
| `Common/Security` | `Mappings` (`CustomClaimTypes`), `Repositories/Interfaces` | Validasi stempel keamanan cookie |
| `Mappings/` | `DTOs/Auth`, `Models`, `Common/Enums` | Konversi objek |
| `DTOs/Auth` | `Constants` (`AuthSettings`) | Batas panjang & pola password |
| `Models/` | `Common/Enums` | `User.Role` bertipe `UserRole` |
| `Views/` | `DTOs/Auth` (sebagai `@model`), `Constants` (`Roles`) | Menampilkan form & menyembunyikan menu sesuai role |

Yang **tidak boleh** terjadi (dan memang tidak ada): `Controllers` → `Repositories`/`Data`, atau `Services` → `Views`/`Controllers`.

### Rantai dependensi runtime (constructor injection)

```text
AccountController
  └─ IAuthService  ──(DI)──►  AuthService
        ├─ IUserRepository  ──►  UserRepository : Repository<User>
        │                            └─ AppDbContext  ──► (UserConfiguration, User)
        ├─ IPasswordHasher<User>  ──►  PasswordHasher<User>   (bawaan ASP.NET Core)
        ├─ IEmailSender  ──►  FakeEmailSender (Development)  |  SmtpEmailSender (lainnya)
        │                         └─ SmtpEmailSender memakai IOptions<SmtpSettings>
        ├─ IHttpContextAccessor  (untuk SignIn/SignOut cookie)
        └─ ILogger<AuthService>
```

---

## 4. Penjelasan Setiap File

### 4.1 `Common/`

| File | Isi dan fungsi |
|---|---|
| `Enums/UserRole.cs` | `enum UserRole { User = 0, Admin = 1 }`. Disimpan sebagai `int` di kolom `Role`. Nama enum ini juga dipakai sebagai nama role di claim (`user.Role.ToString()`), jadi harus sama persis dengan string di `Constants/Roles.cs`. |
| `Security/SecurityStampCookieValidator.cs` | `static class` dengan method `ValidateAsync(CookieValidatePrincipalContext)`. Dipasang ke event cookie `OnValidatePrincipal` di `Program.cs`. Tiap request yang membawa cookie: ambil claim `security_stamp` dan `NameIdentifier`, ambil user dari `IUserRepository`, bandingkan stamp. Kalau user hilang atau stamp beda → `RejectPrincipal()` + `SignOutAsync` (user dianggap logout). Inilah yang membuat reset password otomatis mengeluarkan sesi di perangkat lain. |
| `Settings/SmtpSettings.cs` | POCO (`Host`, `Port`, `User`, `Password`, `FromAddress`, `FromName`). Diisi dari section `"Smtp"` lewat `Configure<SmtpSettings>` dan dibaca `SmtpEmailSender` via `IOptions<SmtpSettings>`. |

### 4.2 `Constants/`

| File | Isi dan fungsi |
|---|---|
| `AuthSettings.cs` | `MinPasswordLength = 8`, `MaxPasswordLength = 128`, `PasswordPattern` (regex harus ada huruf **dan** angka), `MaxFailedAttempts = 5`, `LockoutMinutes = 10`, `ResetTokenLifetime = 1 jam`. Dipakai `RegisterDto`, `ResetPasswordDto`, dan `AuthService`. |
| `CookieNames.cs` | `Auth = "NetMvcAuthV2.Auth"`, nama cookie sesi login. Dipakai `Program.cs`. |
| `RateLimitPolicies.cs` | `Auth = "auth"`, `ForgotPassword = "forgot-password"`. Nama policy didefinisikan di `Program.cs`, dipakai lewat atribut `[EnableRateLimiting(...)]` di `AccountController`. |
| `Roles.cs` | `Admin = "Admin"`, `User = "User"` (string). Untuk `[Authorize(Roles = Roles.Admin)]` dan `User.IsInRole(...)` di view. |

### 4.3 `Models/`

| File | Isi dan fungsi |
|---|---|
| `User.cs` | Entity tabel `Users`: `Id`, `Email`, `FullName`, `PasswordHash`, `Role`, `ResetPasswordTokenHash`, `PasswordResetTokenExpireAt`, `SecurityStamp`, `FailedLoginCount`, `LockoutEndAt`, `CreatedAt`. |
| `ErrorViewModel.cs` | `RequestId` untuk halaman error (`Views/Shared/Error.cshtml`), dipakai `HomeController.Error`. |

### 4.4 `DTOs/Auth/`

Semua pakai Data Annotations dengan pesan error Bahasa Indonesia. DTO dipakai langsung sebagai `@model` view karena bentuknya 1:1 dengan form.

| File | Field | Aturan utama |
|---|---|---|
| `RegisterDto.cs` | `Email`, `FullName`, `Password`, `ConfirmPassword` | Email wajib/valid/maks 256; nama maks 100; password 8–128 + huruf & angka; konfirmasi harus sama (`[Compare]`) |
| `LoginDto.cs` | `Email`, `Password`, `RememberMe` | Email & password wajib |
| `ForgotPasswordDto.cs` | `Email` | Wajib & valid |
| `ResetPasswordDto.cs` | `Token` (hidden), `NewPassword`, `ConfirmPassword` | Token wajib; password sama aturannya dengan register |

### 4.5 `Mappings/UserMappings.cs`

| Member | Fungsi |
|---|---|
| `NormalizeEmail(email)` | `Trim().ToLowerInvariant()`. Satu-satunya tempat normalisasi email; dipakai `AuthService` dan `DbSeeder`. |
| `RegisterDto.ToUser()` | Membuat `User` baru: email dinormalisasi, nama di-trim, **`Role` selalu `UserRole.User`**. `PasswordHash` sengaja kosong — diisi service. |
| `User.ToPrincipal(scheme)` | Membuat `ClaimsPrincipal` berisi claim `NameIdentifier` (Id), `Email`, `Name` (FullName), `Role`, dan `security_stamp`. Dipakai saat sign-in. |
| `CustomClaimTypes.SecurityStamp` | Konstanta `"security_stamp"`. Ada di file yang sama; dipakai juga oleh `SecurityStampCookieValidator`. |

### 4.6 `Data/`

| File | Fungsi |
|---|---|
| `AppDbContext.cs` | Turunan `DbContext`. Punya `DbSet<User> Users`. `OnModelCreating` memanggil `ApplyConfigurationsFromAssembly` sehingga semua `IEntityTypeConfiguration<>` otomatis terbaca (tidak perlu didaftarkan satu-satu). |
| `Configurations/UserConfiguration.cs` | Fluent API untuk `User`: nama tabel `Users`; `Email` required/maks 256/**unique index `IX_Users_Email`**; `FullName` required/maks 100; `PasswordHash` required; `Role` default `User`; `ResetPasswordTokenHash` maks 64 + index `IX_Users_ResetPasswordTokenHash`; `SecurityStamp` required/maks 64; `FailedLoginCount` default 0; `CreatedAt` default `SYSUTCDATETIME()`. |
| `Seed/DbSeeder.cs` | `SeedAdminAsync(IServiceProvider)`: membuat scope sendiri, membaca `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:AdminName` dari konfigurasi. Kalau kosong → dilewati. Kalau email sudah ada → dilewati (idempotent). Selain itu hash password dan simpan user dengan `Role = Admin`. |

### 4.7 `Repositories/`

| File | Fungsi |
|---|---|
| `Interfaces/IRepository.cs` | Kontrak generik: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Remove`, `SaveChangesAsync`. |
| `Interfaces/IUserRepository.cs` | Menambah: `GetByEmailAsync`, `GetByResetTokenHashAsync`, `EmailExistsAsync`. |
| `Implementations/Repository.cs` | `Repository<T>` generik di atas `AppDbContext`. Menyediakan `Set` (`DbSet<T>`) dan `Context` untuk turunannya. |
| `Implementations/UserRepository.cs` | `UserRepository : Repository<User>, IUserRepository`. Query `Users` berdasarkan email / hash token. **Tidak** menormalisasi email — itu tugas lapisan atas. |

### 4.8 `Services/`

| File | Fungsi |
|---|---|
| `Interfaces/IAuthService.cs` | Kontrak `RegisterAsync`, `LoginAsync`, `LogoutAsync`, `RequestPasswordResetAsync`, `ResetPasswordAsync` + `record AuthResult(bool Success, string? Error)`. |
| `Interfaces/IEmailSender.cs` | Kontrak `SendAsync(toEmail, subject, htmlBody)`. Ganti penyedia email cukup dengan implementasi baru. |
| `Implementations/AuthService.cs` | Seluruh logika auth (rincian di bagian 6). |
| `Implementations/SmtpEmailSender.cs` | Kirim email sungguhan lewat **MailKit** (STARTTLS, login SMTP bila `User` diisi). |
| `Implementations/FakeEmailSender.cs` | Untuk Development: tidak mengirim apa pun, hanya menulis isi email (berisi link reset) ke log. |

### 4.9 `Controllers/`

| File | Action | Catatan |
|---|---|---|
| `AccountController.cs` | `Register` GET/POST, `Login` GET/POST, `Logout` POST, `ForgotPassword` GET/POST, `ForgotPasswordConfirmation` GET, `ResetPassword` GET/POST, `AccessDenied` GET | Satu-satunya dependensi: `IAuthService`. Semua POST memakai `[ValidateAntiForgeryToken]`; POST Login/Register memakai `[EnableRateLimiting("auth")]`, POST ForgotPassword memakai `"forgot-password"`. |
| `AdminController.cs` | `Index` | `[Authorize(Roles = Roles.Admin)]` di level class. |
| `HomeController.cs` | `Index`, `Privacy`, `Error` | `Index` tanpa logika; view-nya yang membedakan tamu vs user login. `Error` memakai `ErrorViewModel`. |

### 4.10 Views dan aset: lihat [bagian 7](#7-view-layout-dan-aset-statis).

---

## 5. Program.cs — Penjelasan Penuh

`Program.cs` memakai *top-level statements* (tanpa `class Program`). Isinya terbagi tiga fase: **(A) daftar service**, **(B) build + seeding**, **(C) susun pipeline**.

### 5.0 Konsep lifetime (maksud "scoped")

| Lifetime | Artinya | Contoh di app ini |
|---|---|---|
| **Singleton** | 1 instance untuk seluruh umur aplikasi | `IHttpContextAccessor`, `IOptions<SmtpSettings>`, policy rate limiter |
| **Scoped** | 1 instance **per request HTTP** (dibuang saat request selesai) | `AppDbContext`, semua repository, `AuthService`, email sender, `IPasswordHasher<User>` |
| **Transient** | instance baru tiap kali diminta | tidak dipakai |

Aturan DI: service *scoped* tidak boleh disuntik ke *singleton*. Itu sebabnya `DbSeeder` (jalan di luar request) membuat scope sendiri dengan `CreateScope()`.

### 5.1 Fase A — Pendaftaran service (`builder.Services`)

| Baris di `Program.cs` | Lifetime | Fungsi dan siapa yang memakainya |
|---|---|---|
| `AddControllersWithViews()` | — | Mengaktifkan MVC: controller + Razor view + tag helper + model binding. Tanpa ini `AccountController` tidak ditemukan. |
| `AddDbContext<AppDbContext>(UseSqlServer(GetConnectionString("DefaultConnection")))` | **Scoped** | Mendaftarkan `AppDbContext`. Connection string dibaca dari `appsettings.json` (`ConnectionStrings:DefaultConnection`). Dipakai `Repository<T>` dan `DbSeeder`. |
| `AddScoped(typeof(IRepository<>), typeof(Repository<>))` | **Scoped** | Generic terbuka: meminta `IRepository<X>` otomatis dibuatkan `Repository<X>`. |
| `AddScoped<IUserRepository, UserRepository>()` | **Scoped** | Meminta `IUserRepository` → dapat `UserRepository`. Dipakai `AuthService` dan `SecurityStampCookieValidator`. |
| `AddHttpContextAccessor()` | **Singleton** | Membuat `IHttpContextAccessor` tersedia. `AuthService` memakainya untuk `SignInAsync`/`SignOutAsync` (service tidak punya akses `HttpContext` langsung seperti controller). |
| `AddScoped<IPasswordHasher<User>, PasswordHasher<User>>()` | **Scoped** | Hasher password bawaan ASP.NET Core (PBKDF2 + salt acak). Didaftarkan lewat interface agar bisa diganti di test. Dipakai `AuthService` dan `DbSeeder`. |
| `AddScoped<IAuthService, AuthService>()` | **Scoped** | Layanan auth. Dipakai `AccountController`. |
| `Configure<SmtpSettings>(GetSection("Smtp"))` | Singleton (options) | Mengikat section `"Smtp"` (`appsettings.json` + user-secrets) ke `SmtpSettings`. Dibaca `SmtpEmailSender`. |
| `if (IsDevelopment) AddScoped<IEmailSender, FakeEmailSender>() else AddScoped<IEmailSender, SmtpEmailSender>()` | **Scoped** | Memilih implementasi email berdasarkan environment. Controller/service tidak tahu mana yang aktif. |
| `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` | — | Mendaftarkan skema autentikasi cookie. Opsi cookie di bawah. |
| `AddRateLimiter(...)` | Singleton | Mendefinisikan dua policy pembatas request. Rinci di bawah. |

**Opsi cookie (`AddCookie`):**

| Opsi | Nilai | Maksud |
|---|---|---|
| `Cookie.Name` | `CookieNames.Auth` | Nama cookie dari konstanta |
| `LoginPath` | `/Account/Login` | Tujuan redirect kalau membuka halaman `[Authorize]` tanpa login (`?ReturnUrl=` ditambahkan otomatis) |
| `LogoutPath` | `/Account/Logout` | Path logout yang dikenali skema (action-nya sendiri POST) |
| `AccessDeniedPath` | `/Account/AccessDenied` | Tujuan redirect kalau sudah login tapi role tidak cukup |
| `Cookie.HttpOnly` | `true` | Cookie tidak bisa dibaca JavaScript |
| `Cookie.SameSite` | `Lax` | Cookie tidak ikut request lintas-situs (kecuali navigasi GET biasa) → mitigasi CSRF |
| `Cookie.SecurePolicy` | `Always` | Cookie hanya dikirim lewat **https** |
| `ExpireTimeSpan` | 14 hari | Umur tiket auth |
| `SlidingExpiration` | `true` | Umur diperpanjang selama user aktif |
| `Events.OnValidatePrincipal` | `SecurityStampCookieValidator.ValidateAsync` | Dijalankan di tiap request ber-cookie untuk memeriksa `SecurityStamp` ke DB |

**Rate limiter:**

| Policy | Batas | Dipakai oleh |
|---|---|---|
| `"auth"` (`RateLimitPolicies.Auth`) | 10 request / 1 menit per IP | `Register` POST, `Login` POST |
| `"forgot-password"` (`RateLimitPolicies.ForgotPassword`) | 3 request / 15 menit per IP | `ForgotPassword` POST |

Tipe: *fixed window*, `QueueLimit = 0` (request berlebih langsung ditolak). Kunci partisi = IP klien dari fungsi lokal `ClientIp`.
Respons saat terlampaui: **HTTP 429**.

### 5.2 Fase B — Build dan seeding

| Baris | Fungsi |
|---|---|
| `var app = builder.Build();` | Menutup pendaftaran service dan membangun aplikasi. Setelah ini `builder.Services` tidak bisa diubah. |
| `if (IsDevelopment) await DbSeeder.SeedAdminAsync(app.Services);` | Hanya di Development. Berjalan **sekali saat startup**, sebelum menerima request. Membuat scope sendiri → ambil `AppDbContext` + `IPasswordHasher<User>` → buat Admin kalau belum ada. Karena hanya saat startup, mengubah user-secrets saat app jalan **tidak** berpengaruh sampai app dijalankan ulang. |

### 5.3 Fase C — Pipeline middleware (urutan = urutan eksekusi request)

```text
Request masuk
  │
  ├─ (hanya non-Development) UseExceptionHandler("/Home/Error")   tangkap exception → halaman error
  ├─ (hanya non-Development) UseHsts()                            paksa browser selalu https
  ├─ UseHttpsRedirection()      redirect http → https (bila port https diketahui)
  ├─ UseRouting()               cocokkan URL ke endpoint (controller/action) — harus SEBELUM yang di bawahnya
  ├─ UseRateLimiter()           terapkan [EnableRateLimiting] milik endpoint terpilih (butuh routing dulu)
  ├─ UseAuthentication()        baca cookie → bangun User; di sini OnValidatePrincipal berjalan
  ├─ UseAuthorization()         cek [Authorize] / Roles; WAJIB setelah UseAuthentication
  ├─ MapStaticAssets()          layani file wwwroot (css/js/img/lib) dengan fingerprint & cache
  └─ MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets()
        └─ endpoint: Controller → Action → View
```

- **Mengapa `UseAuthentication` sebelum `UseAuthorization`:** otorisasi butuh tahu siapa usernya; kalau urutannya terbalik semua orang dianggap anonim.
- **Mengapa `UseRateLimiter` sesudah `UseRouting`:** policy ditempel di endpoint lewat atribut, jadi endpoint harus sudah dipilih router.
- **Route default** `{controller=Home}/{action=Index}/{id?}`: `/` → `HomeController.Index`; `/Account/Login` → `AccountController.Login`; `/Admin` → `AdminController.Index`.
- `app.Run()` menjalankan server dan memblokir sampai aplikasi dimatikan.
- Fungsi lokal `static string ClientIp(HttpContext)` di akhir file: mengembalikan IP klien (atau `"unknown"`), dipakai kedua policy rate limiter.

### 5.4 Peta "file ini terdaftar di bagian Program.cs mana"

| File / class | Didaftarkan di | Lifetime |
|---|---|---|
| `AccountController`, `AdminController`, `HomeController` | `AddControllersWithViews()` (ditemukan otomatis lewat scanning assembly) | dibuat per request |
| `AppDbContext` | `AddDbContext` | Scoped |
| `Repository<T>` | `AddScoped(typeof(IRepository<>), typeof(Repository<>))` | Scoped |
| `UserRepository` | `AddScoped<IUserRepository, UserRepository>` | Scoped |
| `PasswordHasher<User>` | `AddScoped<IPasswordHasher<User>, PasswordHasher<User>>` | Scoped |
| `AuthService` | `AddScoped<IAuthService, AuthService>` | Scoped |
| `FakeEmailSender` / `SmtpEmailSender` | `if (IsDevelopment) ... else ...` | Scoped |
| `SmtpSettings` | `Configure<SmtpSettings>(GetSection("Smtp"))` | Options (singleton) |
| `SecurityStampCookieValidator` | `AddCookie(... Events.OnValidatePrincipal = ...)` (bukan DI; method static) | — |
| `CookieNames`, `RateLimitPolicies`, `AuthSettings`, `Roles` | dipakai langsung sebagai konstanta (bukan service) | — |
| `DbSeeder` | dipanggil langsung setelah `Build()` (bukan service) | — |
| `UserConfiguration` | tidak di `Program.cs`; ditemukan `ApplyConfigurationsFromAssembly` di `AppDbContext` | — |
| `UserMappings` | tidak didaftarkan; extension method static | — |
| DTO (`RegisterDto`, dst.) | tidak didaftarkan; dibuat otomatis oleh model binding MVC | — |

---

## 6. Alur Setiap Fitur (File ke File)

Notasi: `File.Method` → `File.Method` = memanggil. Tiap alur ditulis dari klik user sampai respons.

### 6.1 Register (`GET/POST /Account/Register`)

**GET (menampilkan form)**

1. Router → `AccountController.Register()` (GET).
2. Jika `User.Identity.IsAuthenticated` → `RedirectToAction("Index","Home")`.
3. Selain itu `View(new RegisterDto())` → `Views/Account/Register.cshtml` (layout `_AuthLayout`).

**POST (submit form)**

1. Rate limiter policy `"auth"` dicek (maks 10/menit/IP; lebih dari itu → 429).
2. Anti-forgery token divalidasi (`[ValidateAntiForgeryToken]`; token disisipkan tag helper `<form asp-action>`).
3. MVC *model binding* mengisi `RegisterDto` dari form; Data Annotations menghasilkan `ModelState`.
4. `AccountController.Register(dto)`:
   - Sudah login → redirect Home.
   - `ModelState` valid → `IAuthService.RegisterAsync(dto)`.
5. `AuthService.RegisterAsync`:
   1. `UserMappings.NormalizeEmail(dto.Email)`
   2. `IUserRepository.EmailExistsAsync(email)` → `UserRepository` → `AppDbContext.Users.AnyAsync`
   3. Jika ada → `AuthResult(false, "Email sudah terdaftar.")`
   4. `dto.ToUser()` (`UserMappings`): role = `User`
   5. `IPasswordHasher<User>.HashPassword` → isi `PasswordHash`
   6. `AddAsync` + `SaveChangesAsync`. Bila `DbUpdateException` (dua register bersamaan, kena unique index) dan emailnya memang sudah ada → `"Email sudah terdaftar."`, selain itu exception dilempar ulang.
   7. `AuthResult(true)`
6. Kembali di controller:
   - Sukses → `TempData["SuccessMessage"] = "Registrasi berhasil, silakan login."` → redirect ke `Login`.
   - Gagal → error ditempel ke field `Email`; password & konfirmasi dikosongkan; `View(dto)` ditampilkan ulang (input lain tetap terisi).
7. Di `Login`, partial `_FlashMessages` membaca `TempData["SuccessMessage"]` dan menampilkannya.

### 6.2 Login (`GET/POST /Account/Login?returnUrl=...`)

**POST**

1. Rate limit `"auth"` → anti-forgery → binding `LoginDto`.
2. `AccountController.Login(dto, returnUrl)` → `IAuthService.LoginAsync(dto)`.
3. `AuthService.LoginAsync`:
   1. Normalisasi email → `IUserRepository.GetByEmailAsync`.
   2. **User tidak ada** → tetap menjalankan `VerifyHashedPassword` terhadap hash dummy (`_dummyHash`, supaya waktu respons sama) → `"Email atau password salah."`
   3. **Sedang terkunci** (`LockoutEndAt > sekarang`) → `"Terlalu banyak percobaan. Coba lagi beberapa menit lagi."` (password benar pun ditolak).
   4. Lockout sudah lewat → `LockoutEndAt = null`, `FailedLoginCount = 0`.
   5. `VerifyHashedPassword`:
      - **Failed** → `FailedLoginCount++`; bila ≥ 5 → `LockoutEndAt = sekarang + 10 menit`; simpan; kembalikan pesan lockout (jika baru terkunci) atau pesan generik.
      - **SuccessRehashNeeded** → hash ulang & simpan hash baru.
      - **Success** → lanjut.
   6. `FailedLoginCount = 0`, `LockoutEndAt = null`, simpan.
   7. `AuthenticationProperties`: `IsPersistent = RememberMe`; jika "Ingat saya" → `ExpiresUtc = +14 hari` (cookie persisten), kalau tidak → cookie sesi (hilang saat browser ditutup).
   8. `user.ToPrincipal(...)` (`UserMappings`) → claim Id/Email/Name/Role/security_stamp.
   9. `HttpContext.SignInAsync(cookie scheme, principal, properties)` → server menulis cookie `NetMvcAuthV2.Auth` di response.
4. Controller:
   - Sukses → jika `returnUrl` tidak kosong **dan** `Url.IsLocalUrl(returnUrl)` → `LocalRedirect(returnUrl)`; selain itu → Home (mencegah *open redirect*, mis. `https://evil.com` diabaikan).
   - Gagal → error masuk validation summary (`ModelState` level model), password dikosongkan, `returnUrl` dipertahankan di `ViewData`.

### 6.3 Setiap request sesudah login (validasi cookie)

1. Browser mengirim cookie `NetMvcAuthV2.Auth`.
2. `UseAuthentication()` mendekripsi cookie → membentuk `HttpContext.User`.
3. Event `OnValidatePrincipal` → `SecurityStampCookieValidator.ValidateAsync`:
   - Ambil `security_stamp` + `NameIdentifier` dari claim.
   - `IUserRepository.GetByIdAsync(id)` (diambil dari `RequestServices` karena ini scope request yang sama).
   - Stamp beda / user hilang → `RejectPrincipal()` + `SignOutAsync` → user dianggap tamu.
4. `UseAuthorization()` mengevaluasi `[Authorize]`.

Konsekuensi: **reset password di perangkat A** mengganti `SecurityStamp` di DB → di perangkat B request berikutnya ditolak → otomatis logout.

### 6.4 Logout (`POST /Account/Logout`)

1. Tombol "Keluar" ada di dropdown akun `_Layout.cshtml` sebagai `<form asp-controller="Account" asp-action="Logout" method="post">` (anti-forgery otomatis).
2. `AccountController.Logout()` → `IAuthService.LogoutAsync()` → `HttpContext.SignOutAsync` (menghapus cookie).
3. Redirect ke Home. Akses lewat GET → 404 (action hanya menerima POST).

### 6.5 Lupa password (`POST /Account/ForgotPassword`)

1. Rate limit `"forgot-password"` (3 per 15 menit per IP) → anti-forgery → binding `ForgotPasswordDto`.
2. Controller membangun `resetLinkBase` = URL absolut ke action `ResetPassword` (`Url.Action(..., protocol: Request.Scheme)`), lalu memanggil `IAuthService.RequestPasswordResetAsync(email, resetLinkBase)`.
3. `AuthService.RequestPasswordResetAsync`:
   1. Normalisasi email → `GetByEmailAsync`. Tidak ada → **return diam-diam** (tidak error, tidak kirim email).
   2. Token = `RandomNumberGenerator.GetBytes(32)` → hex (64 karakter).
   3. Simpan **hash SHA-256** token ke `ResetPasswordTokenHash`, `PasswordResetTokenExpireAt = sekarang + 1 jam` (token lama otomatis tertimpa).
   4. Bangun link `resetLinkBase?token=<token mentah>` dan isi email HTML.
   5. `IEmailSender.SendAsync(...)`: Development → `FakeEmailSender` menulis ke log; selain itu `SmtpEmailSender` (MailKit). Jika gagal kirim → hanya di-log, tidak dilempar (agar respons sama untuk email terdaftar dan tidak).
4. Controller **selalu** redirect ke `ForgotPasswordConfirmation` (pola Post-Redirect-Get) → pesan generik "Jika email tersebut terdaftar, tautan reset telah kami kirim." Tidak ada user enumeration.

Token mentah hanya ada di link email; di DB hanya hash-nya.

### 6.6 Reset password (`GET/POST /Account/ResetPassword?token=...`)

**GET:** `AccountController.ResetPassword(token)` menambah header `Referrer-Policy: no-referrer` (token di URL tidak bocor lewat header `Referer`), lalu menampilkan `ResetPassword.cshtml` dengan `Token` di input hidden.

**POST:**

1. Anti-forgery → binding `ResetPasswordDto` → validasi aturan password.
2. `IAuthService.ResetPasswordAsync(dto)`:
   1. Hash SHA-256 `dto.Token` → `IUserRepository.GetByResetTokenHashAsync`.
   2. Tidak ketemu **atau** expiry kosong **atau** sudah lewat → `"Token reset password tidak valid atau sudah kedaluwarsa."` (satu pesan untuk semua penyebab).
   3. Valid → hash password baru; `ResetPasswordTokenHash = null`, `PasswordResetTokenExpireAt = null` (**sekali pakai**); `SecurityStamp = Guid baru` (menendang semua sesi lama); simpan; log "reset berhasil" (tanpa token/password).
3. Controller: sukses → `TempData["SuccessMessage"] = "Password berhasil diubah, silakan login."` → redirect Login. Gagal → error ke validation summary, password dikosongkan.

### 6.7 Halaman terlindungi, Access Denied, dan Admin

| Situasi | Yang terjadi |
|---|---|
| Tamu membuka `/Admin` | `[Authorize]` gagal → cookie handler redirect ke `/Account/Login?ReturnUrl=%2FAdmin` |
| User biasa membuka `/Admin` | Login valid tapi role `User` ≠ `Admin` → redirect ke `/Account/AccessDenied` (`AccessDenied.cshtml`) |
| Admin membuka `/Admin` | `AdminController.Index` → `Views/Admin/Index.cshtml` |

Cocoknya role: `AuthService` menaruh `user.Role.ToString()` ("Admin"/"User") ke claim `Role`; `[Authorize(Roles = Roles.Admin)]` membandingkan dengan string `"Admin"`. **Nama enum `UserRole` dan string di `Roles` harus tetap sama.**

### 6.8 Beranda (`/`)

`HomeController.Index` → `Views/Home/Index.cshtml`. Isi view bercabang:

- **Tamu:** landing page (hero + tombol Daftar/Masuk + tiga kartu fitur).
- **Sudah login:** dashboard — banner sambutan, kartu Informasi Akun (nama/email/peran dari claim), Keamanan Akun, Hak Akses (tombol ke Panel Admin hanya untuk Admin).

Seluruh data dashboard berasal dari **claim** di cookie (`User.Identity.Name`, `ClaimTypes.Email`, `User.IsInRole`), tanpa query DB.

### 6.9 Startup aplikasi (urutan lengkap)

```text
dotnet run
  1. WebApplication.CreateBuilder       → baca appsettings.json, appsettings.{Env}.json, user-secrets (Development), env var
  2. builder.Services.*                 → daftar service (bagian 5.1)
  3. builder.Build()
  4. DbSeeder.SeedAdminAsync            → (Development) buat Admin bila belum ada
  5. app.Use* / Map*                    → susun pipeline (bagian 5.3)
  6. app.Run()                          → mulai menerima request
```

### 6.10 Error handling

Non-Development: exception → `UseExceptionHandler("/Home/Error")` → `HomeController.Error` → `Views/Shared/Error.cshtml` dengan `ErrorViewModel.RequestId`. Development: halaman exception developer bawaan.

---

## 7. View, Layout, dan Aset Statis

### 7.1 Mekanisme Razor

| File | Fungsi |
|---|---|
| `Views/_ViewStart.cshtml` | Dijalankan sebelum **setiap** view; menetapkan `Layout = "_Layout"`. View auth menimpa dengan `Layout = "_AuthLayout"`. |
| `Views/_ViewImports.cshtml` | `@using System.Security.Claims`, `@using NetMvcAuthV2`, `@using NetMvcAuthV2.Models`, dan `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` (mengaktifkan `asp-for`, `asp-action`, `asp-validation-for`, dst.). |

Konvensi lokasi: `AccountController.Login` mencari `Views/Account/Login.cshtml`, lalu `Views/Shared/Login.cshtml`.

### 7.2 `Views/Shared/`

| File | Fungsi |
|---|---|
| `_Layout.cshtml` | Layout aplikasi utama (halaman Home, Admin, Privacy). Navbar sticky: logo+nama, menu Beranda (dan Panel Admin bila Admin), dropdown akun (avatar, nama, email, badge peran, tombol Keluar) atau tombol Masuk/Daftar. Menyertakan `_FlashMessages`, footer, jQuery + Bootstrap bundle, `site.js`, dan `@RenderSection("Scripts")`. |
| `_AuthLayout.cshtml` | Layout halaman auth tanpa navbar/footer. Desktop terbelah dua: panel branding kiri (logo + tagline, link ke Home) dan form kanan; HP: logo kecil di atas form saja. Memuat `auth.js` setelah section Scripts. |
| `_FlashMessages.cshtml` | Partial pesan satu-kali dari `TempData["SuccessMessage"]` (alert hijau, `role="status"`) dan `["ErrorMessage"]` (alert merah, `role="alert"`). |
| `_ValidationScriptsPartial.cshtml` | Memuat jquery-validation + unobtrusive untuk validasi sisi klien. Dipanggil dari `@section Scripts` tiap view berformulir. |
| `Error.cshtml` | Halaman error (bawaan template), model `ErrorViewModel`. |
| `_Layout.cshtml.css`, `_AuthLayout.cshtml.css` | **Scoped CSS**: CSS yang hanya berlaku untuk elemen yang dirender langsung oleh layout tersebut. Saat build, ASP.NET menggabungkannya jadi `NetMvcAuthV2.styles.css` dan menambahkan atribut `b-xxxx` ke elemen. Elemen yang datang dari `RenderBody()` **tidak** ikut tersentuh — itu sebabnya gaya umum ada di `site.css`. |

Awalan `_` pada nama file = *bukan halaman yang bisa dituju langsung* (layout/partial/pengaturan).

### 7.3 `Views/Account/`

| View | Model | Isi dan tujuan |
|---|---|---|
| `Register.cshtml` | `RegisterDto` | 4 field, toggle "Tampilkan" password, hint password, link ke Login |
| `Login.cshtml` | `LoginDto` | Email, password, "Ingat saya", link "Lupa password?" dan "Daftar"; `asp-route-returnUrl` membawa `ReturnUrl` ke POST |
| `ForgotPassword.cshtml` | `ForgotPasswordDto` | Satu field email |
| `ForgotPasswordConfirmation.cshtml` | — | Pesan generik + tombol kembali ke Login |
| `ResetPassword.cshtml` | `ResetPasswordDto` | Token hidden, password baru + ulangi, hint |
| `AccessDenied.cshtml` | — | Pesan 403 + tombol ke Home |

Pola yang sama di view berformulir: `data-auth-form` (dikenali `auth.js`), `asp-validation-summary="ModelOnly"` untuk error umum, `asp-validation-for` per field, atribut `autocomplete` yang benar (`email`, `current-password`, `new-password`).

### 7.4 View lain

| View | Fungsi |
|---|---|
| `Home/Index.cshtml` | Landing (tamu) atau dashboard (login) — lihat 6.8 |
| `Home/Privacy.cshtml` | Halaman privasi (isi masih bawaan template) |
| `Admin/Index.cshtml` | Panel Admin (saat ini placeholder: info sesi + catatan fitur belum tersedia) |

### 7.5 `wwwroot/`

| File | Fungsi |
|---|---|
| `css/site.css` | Gaya global: warna brand (`--app-primary`), override `.btn-primary`/`.btn-outline-primary`, komponen `.app-*` (navbar, avatar, hero, banner, kartu, ikon), penyembunyi `.validation-summary-valid`. |
| `js/auth.js` | Tiga hal: (1) tombol "Tampilkan/Sembunyikan" password (`data-toggle-password`); (2) menonaktifkan tombol submit setelah klik, hanya jika form lolos validasi klien (cegah double-submit); (3) mengaktifkan lagi tombol saat halaman dipulihkan dari cache (tombol Back, event `pageshow`). |
| `js/site.js` | Kosong (bawaan template), dimuat `_Layout`. |
| `img/logo.svg`, `img/logo-light.svg` | Logo perisai + lubang kunci. Versi `-light` untuk latar biru tua. Juga dipakai sebagai favicon. |
| `lib/` | Bootstrap, jQuery, jquery-validation(+unobtrusive) — pustaka pihak ketiga, jangan diedit. |

`wwwroot` dilayani oleh `MapStaticAssets()` (URL `/css/site.css`, `/img/logo.svg`, ...). Tag `asp-append-version="true"` menambah hash di URL agar cache browser otomatis diperbarui saat file berubah.

---

## 8. Database dan Migration

Tabel **`Users`** (dibuat oleh migration `AddUser`):

| Kolom | Tipe SQL | Aturan |
|---|---|---|
| `Id` | int | PK, identity |
| `Email` | nvarchar(256) | wajib, **unique index** `IX_Users_Email`, disimpan lowercase |
| `FullName` | nvarchar(100) | wajib |
| `PasswordHash` | nvarchar(max) | wajib; output `PasswordHasher<User>` |
| `Role` | int | default 0 (`User`); 1 = `Admin` |
| `ResetPasswordTokenHash` | nvarchar(64) | boleh null; index `IX_Users_ResetPasswordTokenHash`; SHA-256 hex |
| `PasswordResetTokenExpireAt` | datetime2 | boleh null; UTC |
| `SecurityStamp` | nvarchar(64) | wajib; diganti saat reset password |
| `FailedLoginCount` | int | default 0 |
| `LockoutEndAt` | datetime2 | boleh null; UTC |
| `CreatedAt` | datetime2 | default `SYSUTCDATETIME()` |

Perintah umum (jalankan dari folder project):

```bash
dotnet ef migrations add NamaMigration   # buat migration baru setelah mengubah Model/Configuration
dotnet ef database update                # terapkan migration ke database
dotnet ef migrations remove              # batalkan migration terakhir (yang belum di-update)
```

Satu perubahan skema = satu migration. Folder `Migrations/` dibuat/diubah oleh perintah di atas — jangan diedit tangan.

---

## 9. Konfigurasi dan Secret

Urutan sumber konfigurasi (yang belakangan menimpa yang awal): `appsettings.json` → `appsettings.{Environment}.json` → **user-secrets** (hanya Development) → environment variable → argumen command line.

| Kunci | Sumber yang dianjurkan | Dipakai oleh |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `appsettings.json` (tanpa password; memakai Windows auth) | `AddDbContext` di `Program.cs` |
| `Smtp:Host`, `Smtp:Port`, `Smtp:FromAddress`, `Smtp:FromName` | `appsettings.json` | `SmtpSettings` → `SmtpEmailSender` |
| `Smtp:User`, `Smtp:Password` | **user-secrets** / env var (`Smtp__User`) | `SmtpEmailSender` |
| `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:AdminName` | **user-secrets** | `DbSeeder` |
| `AllowedHosts` | `appsettings.json` | Filter Host bawaan ASP.NET Core |

`launchSettings.json` hanya untuk pengembangan lokal: profil `http` (`http://localhost:5050`) dan `https` (`https://localhost:7038`), keduanya `ASPNETCORE_ENVIRONMENT=Development`.
Cookie bersifat `Secure`, jadi login hanya berfungsi lewat **https**.

---

## 10. Keamanan: Fitur Mana Ada di File Mana

| Perlindungan | Implementasi | File |
|---|---|---|
| Hash password (PBKDF2 + salt) | `IPasswordHasher<User>`; rehash otomatis bila `SuccessRehashNeeded` | `AuthService.cs`, `Program.cs` |
| Token reset acak & hanya hash disimpan | `RandomNumberGenerator` 32 byte + SHA-256 | `AuthService.cs` |
| Token sekali pakai, kedaluwarsa 1 jam | Null-kan token+expiry; cek `expireAt < UtcNow` | `AuthService.cs`, `AuthSettings.cs` |
| Anti user-enumeration | Pesan login generik; forgot selalu respons sama; kegagalan kirim email ditelan | `AuthService.cs`, `AccountController.cs` |
| Anti timing attack | Hash dummy untuk email tak dikenal | `AuthService.cs` |
| Anti brute-force | Lockout 5x/10 menit per akun + rate limiter per IP | `AuthService.cs`, `AuthSettings.cs`, `Program.cs`, `AccountController.cs` |
| Sesi dibatalkan saat ganti password | `SecurityStamp` + validator cookie | `AuthService.cs`, `SecurityStampCookieValidator.cs`, `UserMappings.cs` |
| Cookie aman | HttpOnly, Secure, SameSite=Lax, sliding 14 hari | `Program.cs` |
| CSRF | `[ValidateAntiForgeryToken]` di semua POST + tag helper form | `AccountController.cs`, view |
| Open redirect | `Url.IsLocalUrl(returnUrl)` | `AccountController.cs` |
| Over-posting / privilege escalation | Bind ke DTO; `Role` tak ada di DTO; `ToUser()` mengunci role default | `DTOs/Auth/*`, `UserMappings.cs` |
| Email unik | Cek di service + unique index DB (tahan race condition) | `AuthService.cs`, `UserConfiguration.cs` |
| Otorisasi di server | `[Authorize(Roles = Roles.Admin)]`; menu hanya disembunyikan di UI sebagai tambahan | `AdminController.cs`, `_Layout.cshtml` |
| Token tidak bocor lewat Referer | Header `Referrer-Policy: no-referrer` | `AccountController.ResetPassword` |
| Tidak ada secret di repo | user-secrets untuk SMTP & admin seed | `README.md`, `DbSeeder.cs` |
| Log tidak memuat password/token | Hanya Id/email/jumlah percobaan yang di-log | `AuthService.cs` |
| Transport | `UseHttpsRedirection`, `UseHsts` (non-Development) | `Program.cs` |

---

## 11. Testing

Project `tests/NetMvcAuthV2.Tests` (xUnit + NSubstitute), dijalankan dengan `dotnet test tests/NetMvcAuthV2.Tests`.

| File | Yang diuji |
|---|---|
| `AuthServiceTests.cs` | `AuthService` dengan `IUserRepository` (palsu berbasis `List<User>`), `IEmailSender`, dan `IAuthenticationService` di-mock; `PasswordHasher<User>` asli. Mencakup: register baru/duplikat (beda huruf besar-kecil), login benar (cek claim lengkap), "Ingat saya" (persisten atau tidak), password salah (counter naik), email tak dikenal (pesan sama), lockout ke-5 (login benar tetap ditolak), lockout kedaluwarsa (counter reset), forgot (hash tersimpan, 1 email terkirim, token cocok dengan hash), forgot email tak ada (tidak ada email), kegagalan SMTP (tidak bocor), reset valid/kedaluwarsa/salah/sudah dipakai. |
| `UserMappingsTests.cs` | Normalisasi email, trim nama, role default, `RegisterDto` tak punya properti `Role`, dan claim hasil `ToPrincipal`. |

Cara test membuat `HttpContext` untuk `SignInAsync`: `DefaultHttpContext` dengan `RequestServices` berisi `IAuthenticationService` palsu, dibungkus `IHttpContextAccessor` palsu.

**Belum ada:** test UI browser headless (alur end-to-end) — lihat bagian 14.

---

## 12. Glosarium Nama Folder dan File

| Nama | Arti / alasan penamaan |
|---|---|
| **Controllers** | Pengendali; kelas yang menerima request HTTP dan memutuskan respons. Akhiran `Controller` wajib agar MVC mengenalinya. |
| **Services** | Layanan; menampung *business logic*. Dipisah dari controller supaya controller tetap tipis dan logika bisa diuji. |
| **Repositories** | Pola Repository: menyembunyikan detail query/EF di balik interface. |
| **Interfaces / Implementations** | Kontrak vs isi. Kode memanggil kontrak (`IAuthService`), DI menyuntikkan isinya. |
| **DTOs** | *Data Transfer Object*; wadah data antar lapisan, sekaligus tempat aturan validasi form. Akhiran `Dto`. |
| **Mappings** | Tempat konversi objek (DTO→Model, Model→claims) agar tidak berserakan di service. |
| **Models** | Entity yang dipetakan ke tabel database. |
| **Data** | Semua hal EF Core: `DbContext`, konfigurasi tabel, seeding. |
| **Configurations** | Kelas `IEntityTypeConfiguration<T>`: aturan kolom/index per entity (Fluent API). Akhiran `Configuration`. |
| **Seed** | Data awal; di sini akun Admin. |
| **Migrations** | Riwayat versi skema database (otomatis dari `dotnet ef`). Nama diawali timestamp agar terurut. |
| **Constants** | Nilai tetap yang diketahui saat compile (`const`), dipusatkan agar tidak ada "magic string". |
| **Common** | Kode umum lintas fitur (enum, settings, helper keamanan). |
| **Views** | Template Razor `.cshtml`; subfolder bernama sama dengan controller (`Account`, `Home`, `Admin`). |
| **Shared** | View yang dipakai banyak controller (layout, partial). |
| **wwwroot** | "Web root": satu-satunya folder yang boleh diunduh browser langsung. |
| **Properties** | Berisi `launchSettings.json` (profil jalankan lokal). |
| **tests** | Project pengujian terpisah. |
| Awalan **`I`** (`IAuthService`) | Konvensi C# untuk interface. |
| Awalan **`_`** (`_Layout`, `_FlashMessages`) | View yang bukan halaman sendiri: layout/partial/pengaturan. |
| Akhiran **`.cshtml.css`** | Scoped CSS milik view dengan nama yang sama. |
| **`.Designer.cs`** | File metadata yang dibuat otomatis oleh EF untuk sebuah migration. |
| **`*Tests`** | Kelas pengujian xUnit (`[Fact]`, `[Theory]`). |
| **`AuthResult`** | Hasil standar service: `Success` + pesan `Error` opsional, supaya service tidak melempar exception untuk kegagalan "normal". |
| **SecurityStamp** | String acak per user; berubah saat password diganti. Disimpan di DB dan di cookie; kalau tak cocok, cookie ditolak. |

---

## 13. Cara Menambah Fitur Baru

Contoh: fitur "Ubah Profil".

1. **Model/kolom baru?** Ubah `Models/User.cs` + `Data/Configurations/UserConfiguration.cs` → `dotnet ef migrations add ...` → `dotnet ef database update`.
2. **DTO:** buat `DTOs/Auth/UpdateProfileDto.cs` (+ validasi).
3. **Query baru?** Tambah method di `Repositories/Interfaces/IUserRepository.cs` dan `Implementations/UserRepository.cs`.
4. **Logika:** tambah method di `Services/Interfaces/IAuthService.cs` (atau service baru dengan interface sendiri) lalu isi di `Implementations/`.
5. **Controller:** tambah action (GET+POST, `[Authorize]`, `[ValidateAntiForgeryToken]`) — hanya memanggil service.
6. **View:** `Views/Account/UpdateProfile.cshtml`, `@model` memakai DTO.
7. **Service baru?** Daftarkan di `Program.cs` (`AddScoped<IContoh, Contoh>()`).
8. **Test:** tambahkan di `tests/NetMvcAuthV2.Tests`.

Aturan penaruhan class: *data form* → `DTOs`, *aturan bisnis* → `Services`, *query* → `Repositories`, *tabel* → `Models` + `Data/Configurations`, *nilai tetap* → `Constants`, *konversi objek* → `Mappings`, *halaman* → `Views/<NamaController>`.

---

## 14. Catatan dan Keterbatasan Saat Ini

- **Logging:** memakai `ILogger` bawaan, belum Serilog seperti di PRD.
- **Rate limit forgot password** baru **per IP**; belum per email.
- **Pengaturan `Auth` di `appsettings.json`** (PRD §11) tidak dipakai; batas lockout dan sejenisnya ada di `Constants/AuthSettings.cs`.
- **Host header:** link reset dibangun dari host request, sementara `AllowedHosts` masih `*`. Isi dengan domain asli sebelum deploy.
- **Reverse proxy:** rate limiter membaca `RemoteIpAddress`; di belakang proxy perlu `UseForwardedHeaders`.
- **Seeder** hanya berjalan di Development dan hanya saat startup; mengubah `Seed:AdminPassword` setelah admin dibuat tidak mengubah password yang tersimpan.
- **Test UI headless** (alur browser end-to-end) belum ada.
- **Halaman Privacy** masih berisi teks bawaan template.
- **Panel Admin** masih placeholder (manajemen user di luar scope MVP).
- Fitur v2 di PRD (ganti password saat login, edit profil, verifikasi email, 2FA, login sosial, audit log) belum dikerjakan.
