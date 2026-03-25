<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

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
