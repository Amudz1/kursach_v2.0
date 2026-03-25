<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class Subscription extends Model
{
    protected $fillable = ['user_id', 'plan_id', 'starts_at', 'ends_at', 'is_active'];

    protected $casts = [
        'starts_at' => 'date',
        'ends_at'   => 'date',
        'is_active' => 'boolean',
    ];

    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function plan()
    {
        return $this->belongsTo(SubscriptionPlan::class, 'plan_id');
    }

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
