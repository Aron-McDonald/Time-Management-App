﻿using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimeMgmtLibraryCore.Models;

namespace TimeMgmtLibraryCore.DataAccess
{
    public class UserDAL
    {
        private ApplicationDbContext _context;

        public UserDAL()
        {
            _context = new ApplicationDbContext();
        }

        //Creating an object of the user class to assign values to the username and password properties.
        public static UserModel User = new UserModel();

        //Creating an object of this class to use the database (_context)
        public static UserDAL uDAL = new UserDAL();

        //Method for use in the Register view to add a new user to the database.
        public void RegisterNewUser(UserModel userObj)
        {
            //Storing the encrypted password in a temporary variable to pass to the object property.
            var hashedPassword = HashPassword(userObj.Password);
            //Storing the encrypted(Hashed) password in the database.
            userObj.Password = hashedPassword;

            //Adding the new user to the database users(Andrew Troelsen and Philip Japikse, 2017).
            uDAL._context.Users.Add(userObj);
            //Saving the changes made to the database.
            uDAL._context.SaveChanges();
        }

        //Method for use in the LoginPage to check if the username entered exists in the database.
        public bool CheckIfUsernameExists(UserModel userObj)
        {
            bool found = false;

            //Checking if the username which the user has entered exists in the database (Lujan, 2016) & (Andrew Troelsen and Philip Japikse, 2017).
            foreach (var user in uDAL._context.Users)
            {
                found = user.Username.Equals(userObj.Username);
                //If the username is found exit the foreach loop (Lujan, 2016).
                if (found == true)
                {
                    break;
                }
            }
            //Return true if a user with that username was found or return false if it was not.
            return found;
        }

        //Method for use in the LoginPage to check if the password entered matches the username for the user in the database.
        public bool CheckUserCredentials(UserModel userObj)
        {
            bool authorized = false;

            //LINQ query to fetch the user that matches the username entered (Wagner, 2017) & (Andrew Troelsen and Philip Japikse, 2017).
            List<UserModel> userQuery = new List<UserModel>
                (
                    from user in uDAL._context.Users
                    where user.Username.Equals(userObj.Username)
                    select user
                );

            //Checking if the password which the user has entered exists in the database (Lujan, 2016) & (Andrew Troelsen and Philip Japikse, 2017).
            foreach (var user in userQuery)
            {
                authorized = VerifyHashedPassword(user.Password, userObj.Password);
                //If the password matches exit the foreach loop (Lujan, 2016).
                if (authorized == true)
                {
                    break;
                }

            }
            //Return true if a user with matching password was found or return false if it was not.
            return authorized;
        }

        public void LoginUser(UserModel userObj)
        {
            //Setting the username property in the User class to the username of the user logging in
            //for use in other view models to only show the logged in users relevant data and not that of others.
            User.Username = userObj.Username;
        }

        public string GetLoggedInUser()
        {
            return User.Username;
        }

        //The following method was taken from stackoverflow:
        //Author : Michael
        //Link : https://stackoverflow.com/questions/2138429/hash-and-salt-passwords-in-c-sharp

        public string HashPassword(string password)
        {
            var prf = KeyDerivationPrf.HMACSHA256;
            var rng = RandomNumberGenerator.Create();
            const int iterCount = 10000;
            const int saltSize = 128 / 8;
            const int numBytesRequested = 256 / 8;

            // Produce a version 3 (see comment above) text hash.
            var salt = new byte[saltSize];
            rng.GetBytes(salt);
            var subkey = KeyDerivation.Pbkdf2(password, salt, prf, iterCount, numBytesRequested);

            var outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // format marker
            WriteNetworkByteOrder(outputBytes, 1, (uint)prf);
            WriteNetworkByteOrder(outputBytes, 5, iterCount);
            WriteNetworkByteOrder(outputBytes, 9, saltSize);
            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + saltSize, subkey.Length);
            return Convert.ToBase64String(outputBytes);
        }

        //The following method was taken from stackoverflow:
        //Author : Michael
        //Link : https://stackoverflow.com/questions/2138429/hash-and-salt-passwords-in-c-sharp

        public bool VerifyHashedPassword(string hashedPassword, string providedPassword)
        {
            var decodedHashedPassword = Convert.FromBase64String(hashedPassword);

            // If Wrong version
            if (decodedHashedPassword[0] != 0x01)
                return false;

            // Read header information
            var prf = (KeyDerivationPrf)ReadNetworkByteOrder(decodedHashedPassword, 1);
            var iterCount = (int)ReadNetworkByteOrder(decodedHashedPassword, 5);
            var saltLength = (int)ReadNetworkByteOrder(decodedHashedPassword, 9);

            // Read the salt: must be >= 128 bits
            if (saltLength < 128 / 8)
            {
                return false;
            }
            var salt = new byte[saltLength];
            Buffer.BlockCopy(decodedHashedPassword, 13, salt, 0, salt.Length);

            // Read the subkey (the rest of the payload): must be >= 128 bits
            var subkeyLength = decodedHashedPassword.Length - 13 - salt.Length;
            if (subkeyLength < 128 / 8)
            {
                return false;
            }
            var expectedSubkey = new byte[subkeyLength];
            Buffer.BlockCopy(decodedHashedPassword, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

            // Hash the incoming password and verify it
            var actualSubkey = KeyDerivation.Pbkdf2(providedPassword, salt, prf, iterCount, subkeyLength);
            return actualSubkey.SequenceEqual(expectedSubkey);
        }

        //The following method was taken from stackoverflow:
        //Author : Michael
        //Link : https://stackoverflow.com/questions/2138429/hash-and-salt-passwords-in-c-sharp

        private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)(value >> 0);
        }

        //The following method was taken from stackoverflow:
        //Author : Michael
        //Link : https://stackoverflow.com/questions/2138429/hash-and-salt-passwords-in-c-sharp

        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)(buffer[offset + 0]) << 24)
                | ((uint)(buffer[offset + 1]) << 16)
                | ((uint)(buffer[offset + 2]) << 8)
                | ((uint)(buffer[offset + 3]));
        }
    }
}
