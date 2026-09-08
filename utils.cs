using System;
using System.Linq;
using System.Reflection;

namespace Passass.TaskListLocations
{
    public static class Utils
    {
        public static string Capitalize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }

        public static object CreateInstanceAndCallMethod(
            Type type,
            string methodName,
            object[] constructorArgs = null,
            object[] methodArgs = null,
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        )
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            // 1. Создаем экземпляр
            object instance = Activator.CreateInstance(type, constructorArgs ?? Array.Empty<object>());

            if (instance == null)
                throw new Exception($"Не удалось создать экземпляр типа {type.FullName}");

            // 2. Получаем метод
            MethodInfo method = type.GetMethod(methodName, bindingFlags);

            if (method == null)
            {
                // Пробуем найти метод с параметрами
                if (methodArgs != null && methodArgs.Length > 0)
                {
                    Type[] paramTypes = new Type[methodArgs.Length];
                    for (int i = 0; i < methodArgs.Length; i++)
                    {
                        paramTypes[i] = methodArgs[i]?.GetType() ?? typeof(object);
                    }
                    method = type.GetMethod(methodName, bindingFlags, null, paramTypes, null);
                }
            }

            if (method == null)
                throw new MissingMethodException($"Метод {methodName} не найден в типе {type.FullName}");

            // 3. Вызываем метод
            return method.Invoke(instance, methodArgs ?? Array.Empty<object>());
        }

        /// <summary>
        /// Создает экземпляр типа и вызывает метод с возвращаемым значением
        /// </summary>
        public static T CreateInstanceAndCallMethod<T>(
            Type type,
            string methodName,
            object[] constructorArgs = null,
            object[] methodArgs = null,
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        {
            object result = CreateInstanceAndCallMethod(type, methodName, constructorArgs, methodArgs, bindingFlags);
            return result != null ? (T)result : default;
        }
        public static object GetValueByPath(Type rootType, string path, object rootInstance = null)
        {
            if (rootType == null)
                throw new ArgumentNullException(nameof(rootType));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            // Разбиваем путь на части
            string[] parts = path.Split('.');

            object currentInstance = rootInstance;
            Type currentType = rootType;

            foreach (string part in parts)
            {
                if (currentType == null)
                    throw new Exception($"Тип не найден для члена '{part}'");

                // Пробуем получить поле
                FieldInfo field = currentType.GetField(part,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static);

                if (field != null)
                {
                    // Если это статическое поле - instance = null
                    object instance = field.IsStatic ? null : currentInstance;
                    currentInstance = field.GetValue(instance);
                    currentType = currentInstance?.GetType() ?? field.FieldType;
                    continue;
                }

                // Пробуем получить свойство
                PropertyInfo property = currentType.GetProperty(part,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static);

                if (property != null)
                {
                    object instance = property.GetMethod.IsStatic ? null : currentInstance;
                    currentInstance = property.GetValue(instance);
                    currentType = currentInstance?.GetType() ?? property.PropertyType;
                    continue;
                }

                throw new Exception($"Член '{part}' не найден в типе {currentType.FullName}");
            }

            return currentInstance;
        }
        public static BindingFlags FieldTypes = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
        public static string GetLocalizedText(string text) => EFT.LocalizationExtensions.Localized(text);

        public static bool NamespaceExists(string namespaceName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetTypes().Any(type => type.Namespace == namespaceName))
                        return true;
                }
                catch
                {
                    continue;
                }
            }
            return false;
        }

        public static Type GetInternalType(string typeName)
        {
            // Проходим по всем загруженным сборкам
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Пробуем найти тип в сборке
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    continue; // Некоторые сборки могут не давать доступ
                }
            }
            return null;
        }

        public static object CallStaticMethod(Type targetType, string methodName, params object[] parameters)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            // Ищем статический метод
            MethodInfo method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                // Пробуем найти метод с параметрами
                if (parameters != null && parameters.Length > 0)
                {
                    Type[] paramTypes = new Type[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramTypes[i] = parameters[i]?.GetType() ?? typeof(object);
                    }

                    method = targetType.GetMethod(methodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                        null, paramTypes, null);
                }
            }

            if (method == null)
                throw new MissingMethodException($"Статический метод {methodName} не найден в типе {targetType.FullName}");

            // Вызываем статический метод (instance = null)
            return method.Invoke(null, parameters);
        }

        /// <summary>
        /// Вызывает статический метод с возвращаемым значением указанного типа
        /// </summary>
        public static T CallStaticMethod<T>(Type targetType, string methodName, params object[] parameters)
        {
            object result = CallStaticMethod(targetType, methodName, parameters);
            return result != null ? (T)result : default;
        }

        public static object CallMethod(Type targetType, string methodName,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            return CallMethod(targetType, methodName, null, null, bindingFlags);
        }

        /// <summary>
        /// Вызывает метод через рефлексию с параметрами
        /// </summary>
        public static object CallMethod(Type targetType, string methodName,
            object instance, object[] parameters,
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            // Получаем метод
            MethodInfo method = targetType.GetMethod(methodName, bindingFlags);

            if (method == null)
            {
                // Пробуем найти метод с параметрами
                if (parameters != null && parameters.Length > 0)
                {
                    Type[] paramTypes = new Type[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramTypes[i] = parameters[i]?.GetType() ?? typeof(object);
                    }
                    method = targetType.GetMethod(methodName, bindingFlags, null, paramTypes, null);
                }
            }

            if (method == null)
                throw new MissingMethodException($"Метод {methodName} не найден в типе {targetType.FullName}");

            return method.Invoke(instance, parameters);
        }
    }
}