import {TransactionDto} from "./TransactionDto";

export interface AccountDto {
  id: number;
  name2: string;
  transactions: TransactionDto[];
}
