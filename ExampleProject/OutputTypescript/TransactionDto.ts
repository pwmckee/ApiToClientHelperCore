import {AccountDto} from "./AccountDto";

export interface TransactionDto {
  id: number;
  transactor: string;
  date: string;
  amount: number;
  account: AccountDto;
}
