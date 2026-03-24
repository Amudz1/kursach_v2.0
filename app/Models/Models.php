<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Support\Facades\Crypt;

// ── SubscriptionPlan ──────────────────────────────────────────
class SubscriptionPlan extends Model
{
    protected $fillable = ['name', 'duration_days', 'price', 'discount_percent', 'prompt_limit'];

    protected $casts = [
        'price'            => 'decimal:2',
        'discount_percent' => 'decimal:2',
    ];

    public function getFinalPriceAttribute(): float
    {
        return round($this->price * (1 - $this->discount_percent / 100), 2);
    }

    public function getSavingsAttribute(): float
    {
        return round($this->price - $this->final_price, 2);
    }
}

// ── Subscription ──────────────────────────────────────────────
class Subscription extends Model
{
    protected $fillable = ['user_id', 'plan_id', 'starts_at', 'ends_at', 'is_active'];

    protected $casts = [
        'starts_at' => 'date',
        'ends_at'   => 'date',
        'is_active' => 'boolean',
    ];

    public function user()       { return $this->belongsTo(User::class); }
    public function plan()       { return $this->belongsTo(SubscriptionPlan::class, 'plan_id'); }

    public function isActive(): bool
    {
        return $this->is_active && $this->ends_at >= now()->toDateString();
    }

    public function getDaysRemainingAttribute(): int
    {
        if (!$this->isActive()) return 0;
        return max(0, now()->diffInDays($this->ends_at, false));
    }

    public function getStatusTextAttribute(): string
    {
        if (!$this->isActive()) return 'Истекла';
        $days = $this->days_remaining;
        return "Активна до {$this->ends_at->format('d.m.Y')} (осталось {$days} дн.)";
    }
}

// ── PaymentMethod ─────────────────────────────────────────────
class PaymentMethod extends Model
{
    protected $fillable = [
        'user_id', 'card_number_encrypted', 'card_last4',
        'card_holder', 'expire_month', 'expire_year', 'is_default'
    ];

    protected $hidden = ['card_number_encrypted'];

    protected $casts = ['is_default' => 'boolean'];

    public function user() { return $this->belongsTo(User::class); }

    // Шифруем при сохранении
    public function setCardNumberEncryptedAttribute(string $value): void
    {
        $this->attributes['card_number_encrypted'] = Crypt::encryptString($value);
    }

    // Расшифровываем при чтении
    public function getCardNumberDecryptedAttribute(): string
    {
        return Crypt::decryptString($this->card_number_encrypted);
    }

    public function getMaskedNumberAttribute(): string
    {
        return "**** **** **** {$this->card_last4}";
    }
}

// ── Transaction ───────────────────────────────────────────────
class Transaction extends Model
{
    protected $fillable = ['user_id', 'plan_id', 'amount', 'status', 'description'];

    public function user() { return $this->belongsTo(User::class); }
    public function plan() { return $this->belongsTo(SubscriptionPlan::class, 'plan_id'); }
}

// ── Chat ──────────────────────────────────────────────────────
class Chat extends Model
{
    protected $fillable = ['user_id', 'title', 'message_count', 'last_message_at'];

    protected $casts = ['last_message_at' => 'datetime'];

    public function user()     { return $this->belongsTo(User::class); }
    public function messages() { return $this->hasMany(Message::class)->orderBy('created_at'); }

    // Генерируем заголовок из первого промта
    public static function generateTitle(string $firstMessage): string
    {
        $words = explode(' ', strip_tags($firstMessage));
        $title = implode(' ', array_slice($words, 0, 6));
        return mb_strlen($title) > 50 ? mb_substr($title, 0, 50) . '...' : $title;
    }
}

// ── Message ───────────────────────────────────────────────────
class Message extends Model
{
    protected $fillable = [
        'chat_id', 'user_id', 'role', 'content',
        'model_used', 'attachment_path', 'attachment_name'
    ];

    public function chat() { return $this->belongsTo(Chat::class); }
    public function user() { return $this->belongsTo(User::class); }

    public function hasAttachment(): bool
    {
        return !empty($this->attachment_path);
    }
}

// ── RequestLimit ──────────────────────────────────────────────
class RequestLimit extends Model
{
    protected $fillable = ['user_id', 'used_today', 'daily_limit', 'reset_date'];

    public function user() { return $this->belongsTo(User::class); }
}
