<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Support\Facades\Crypt;

class PaymentMethod extends Model
{
    protected $fillable = [
        'user_id', 'card_number_encrypted', 'card_last4',
        'card_holder', 'expire_month', 'expire_year', 'is_default',
    ];

    protected $hidden = ['card_number_encrypted'];

    protected $casts = ['is_default' => 'boolean'];

    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function setCardNumberEncryptedAttribute(string $value): void
    {
        $this->attributes['card_number_encrypted'] = Crypt::encryptString($value);
    }

    public function getCardNumberDecryptedAttribute(): string
    {
        return Crypt::decryptString($this->card_number_encrypted);
    }

    public function getMaskedNumberAttribute(): string
    {
        return "**** **** **** {$this->card_last4}";
    }
}
