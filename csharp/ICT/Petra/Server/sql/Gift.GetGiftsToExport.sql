SELECT PUB_a_gift_batch.*, PUB_a_gift.*, PUB_a_gift_detail.*FROM PUB_a_gift_batch, PUB_a_gift, PUB_a_gift_detailWHERE PUB_a_gift_batch.a_ledger_number_i = ?{#IFDEF BYDATERANGE}AND (PUB_a_gift_batch.a_gl_effective_date_d >= ? AND PUB_a_gift_batch.a_gl_effective_date_d <= ?){#ENDIF BYDATERANGE}{#IFDEF BYBATCHNUMBER}AND (PUB_a_gift_batch.a_batch_number_i >= ? AND PUB_a_gift_batch.a_batch_number_i <= ?){#ENDIF BYBATCHNUMBER}{#IFDEF INCLUDEUNPOSTED}AND (PUB_a_gift_batch.a_batch_status_c = "Posted" OR PUB_a_gift_batch.a_batch_status_c = "Unposted"){#ENDIF INCLUDEUNPOSTED}{#IFNDEF INCLUDEUNPOSTED}AND PUB_a_gift_batch.a_batch_status_c = "Posted"{#ENDIFN INCLUDEUNPOSTED}AND PUB_a_gift.a_ledger_number_i =  PUB_a_gift_batch.a_ledger_number_iAND PUB_a_gift.a_batch_number_i = PUB_a_gift_batch.a_batch_number_i AND PUB_a_gift_detail.a_ledger_number_i = PUB_a_gift_batch.a_ledger_number_iAND PUB_a_gift_detail.a_batch_number_i = PUB_a_gift_batch.a_batch_number_iAND PUB_a_gift_detail.a_gift_transaction_number_i = PUB_a_gift.a_gift_transaction_number_i