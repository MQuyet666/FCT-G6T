def calc_bcc(hex_str):
    """
    Tính BCC (XOR từ byte thứ 2 đến hết) từ chuỗi hex cách nhau bởi dấu cách.
    """
    buff = [int(x, 16) for x in hex_str.strip().split()]
    bcc = 0
    for i in range(1, len(buff)):
        bcc ^= buff[i]
    return bcc

if __name__ == "__main__":
    user_input = input("Nhập chuỗi hex (cách nhau bởi dấu cách): ")
    bcc = calc_bcc(user_input)
    print(f"BCC = {bcc:02X}")
