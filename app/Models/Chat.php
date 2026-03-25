<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class Chat extends Model
{
    protected $fillable = ['user_id', 'title', 'message_count', 'last_message_at'];

    protected $casts = ['last_message_at' => 'datetime'];

    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function messages()
    {
        return $this->hasMany(Message::class)->orderBy('created_at');
    }

    public static function generateTitle(string $firstMessage): string
    {
        $words = explode(' ', strip_tags($firstMessage));
        $title = implode(' ', array_slice($words, 0, 6));
        return mb_strlen($title) > 50 ? mb_substr($title, 0, 50) . '...' : $title;
    }
}
