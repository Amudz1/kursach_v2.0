<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class RequestLimit extends Model
{
    protected $fillable = ['user_id', 'used_today', 'daily_limit', 'reset_date'];

    public function user()
    {
        return $this->belongsTo(User::class);
    }
}
