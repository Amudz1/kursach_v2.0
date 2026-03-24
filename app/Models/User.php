<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;
use Laravel\Sanctum\HasApiTokens;

class User extends Authenticatable
{
    use HasApiTokens, HasFactory, Notifiable;

    protected $fillable = ['username', 'email', 'password', 'is_admin'];

    protected $hidden = ['password', 'remember_token'];

    protected $casts = [
        'email_verified_at' => 'datetime',
        'is_admin'          => 'boolean',
        'password'          => 'hashed',
    ];

    // ── Связи ─────────────────────────────────────────────────
    public function subscriptions()
    {
        return $this->hasMany(Subscription::class);
    }

    public function activeSubscription()
    {
        return $this->hasOne(Subscription::class)
            ->where('is_active', true)
            ->where('ends_at', '>=', now()->toDateString())
            ->latest();
    }

    public function chats()
    {
        return $this->hasMany(Chat::class)->latest();
    }

    public function requestLimit()
    {
        return $this->hasOne(RequestLimit::class);
    }

    public function paymentMethods()
    {
        return $this->hasMany(PaymentMethod::class);
    }

    // ── Хелперы ───────────────────────────────────────────────
    public function hasActiveSubscription(): bool
    {
        return $this->activeSubscription()->exists();
    }

    public function getActiveSubscription(): ?Subscription
    {
        return $this->activeSubscription()->with('plan')->first();
    }

    public function canSendMessage(): bool
    {
        if ($this->hasActiveSubscription()) return true;

        $limit = $this->requestLimit;
        if (!$limit) return true;

        // Сбрасываем счётчик если новый день
        if ($limit->reset_date !== now()->toDateString()) {
            $limit->update(['used_today' => 0, 'reset_date' => now()->toDateString()]);
            return true;
        }

        return $limit->used_today < $limit->daily_limit;
    }

    public function getRemainingPrompts(): int
    {
        if ($this->hasActiveSubscription()) return PHP_INT_MAX;

        $limit = $this->requestLimit;
        if (!$limit) return config('app.free_prompt_limit', 12);

        if ($limit->reset_date !== now()->toDateString()) return $limit->daily_limit;

        return max(0, $limit->daily_limit - $limit->used_today);
    }
}
