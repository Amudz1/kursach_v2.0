<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class Message extends Model
{
    protected $fillable = [
        'chat_id', 'user_id', 'role', 'content',
        'model_used', 'attachment_path', 'attachment_name',
    ];

    public function chat()
    {
        return $this->belongsTo(Chat::class);
    }

    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function hasAttachment(): bool
    {
        return !empty($this->attachment_path);
    }
}
